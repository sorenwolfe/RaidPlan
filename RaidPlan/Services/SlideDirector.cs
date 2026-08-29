using System;
using System.Linq;
using RaidPlan.Model;

namespace RaidPlan.Services;

/// <summary>Why the plan moved, so the planner can show it and the log can explain it.</summary>
public enum SlideChangeReason
{
    CombatStarted,
    CastDetected,
    StepFired,
    Wipe,
    Manual,
}

/// <summary>
/// Decides which slide is on screen during a pull. A known boss cast switches immediately, a step
/// firing its call also moves things along, and a pull starting or wiping goes back to the top.
/// Changing slides by hand parks all of that for a moment.
/// </summary>
public sealed class SlideDirector : IDisposable
{
    private DateTime suppressedUntilUtc = DateTime.MinValue;

    public SlideDirector()
    {
        Plugin.Encounter.CastStarted += OnCastStarted;
        Plugin.Encounter.CombatStarted += OnCombatStarted;
        Plugin.Encounter.Wiped += OnWiped;
        Plugin.Reminders.StepFired += OnStepFired;
    }

    /// <summary>Asks the planner to show a particular slide.</summary>
    public event Action<string, SlideChangeReason>? SlideRequested;

    /// <summary>Asks the planner to go back to the first slide.</summary>
    public event Action<SlideChangeReason>? ResetRequested;

    /// <summary>True while a manual slide change is holding the automation off.</summary>
    public bool IsSuppressed => DateTime.UtcNow < suppressedUntilUtc;

    public float SuppressedFor =>
        IsSuppressed ? (float)(suppressedUntilUtc - DateTime.UtcNow).TotalSeconds : 0f;

    /// <summary>Stand down for a moment after a manual slide change.</summary>
    public void NotifyManualChange()
    {
        var hold = Math.Clamp(Plugin.Config.ManualOverrideSeconds, 0f, 120f);
        suppressedUntilUtc = hold <= 0f ? DateTime.MinValue : DateTime.UtcNow.AddSeconds(hold);
    }

    /// <summary>Hands control straight back to the automation.</summary>
    public void ClearSuppression() => suppressedUntilUtc = DateTime.MinValue;

    private void OnCombatStarted()
    {
        ClearSuppression();

        if (Plugin.Config.AutoAdvanceSlides)
            ResetRequested?.Invoke(SlideChangeReason.CombatStarted);
    }

    private void OnWiped()
    {
        // A wipe is exactly when you want the top of the plan, override or not.
        ClearSuppression();

        if (Plugin.Config.ResetSlidesOnWipe)
            ResetRequested?.Invoke(SlideChangeReason.Wipe);
    }

    private void OnStepFired(TimelineEntry entry)
    {
        if (!Plugin.Config.AutoAdvanceSlides || IsSuppressed)
            return;

        if (!string.IsNullOrEmpty(entry.SlideId))
            SlideRequested?.Invoke(entry.SlideId, SlideChangeReason.StepFired);
    }

    private void OnCastStarted(CastEvent evt)
    {
        if (!Plugin.Config.AutoAdvanceSlides || !Plugin.Config.AutoAdvanceOnCast || IsSuppressed)
            return;

        var plan = Plugin.Plans.Active;
        if (plan == null)
            return;

        var slideId = FindSlideForCast(plan, evt);
        if (!string.IsNullOrEmpty(slideId))
            SlideRequested?.Invoke(slideId!, SlideChangeReason.CastDetected);
    }

    /// <summary>A step written for this exact use of the cast beats a catch-all one.</summary>
    private static string? FindSlideForCast(RaidPlanDocument plan, CastEvent evt)
    {
        var candidates = plan.Timeline
            .Where(e => e.Enabled
                        && e.CastActionId == evt.ActionId
                        && !string.IsNullOrEmpty(e.SlideId)
                        && e.Trigger is TriggerKind.BossCast or TriggerKind.AfterCast or TriggerKind.Predicted)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var exact = candidates.FirstOrDefault(e => e.Occurrence == evt.Occurrence);
        if (exact != null)
            return exact.SlideId;

        // An "every time" step still counts, and so does an AfterCast step anchored here, since
        // the anchor cast is the moment the player needs to be looking at what comes next.
        var any = candidates.FirstOrDefault(e => e.Occurrence <= 0);
        return any?.SlideId;
    }

    public void Dispose()
    {
        Plugin.Encounter.CastStarted -= OnCastStarted;
        Plugin.Encounter.CombatStarted -= OnCombatStarted;
        Plugin.Encounter.Wiped -= OnWiped;
        Plugin.Reminders.StepFired -= OnStepFired;
    }
}
