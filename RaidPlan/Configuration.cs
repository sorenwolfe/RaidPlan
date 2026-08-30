using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using RaidPlan.Model;

namespace RaidPlan;

/// <summary>
/// Per-team presentation and delivery settings. A static that wants terse calls and a static
/// that wants verbose ones can keep separate profiles against the same plan.
/// </summary>
public sealed class TeamProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Default team";

    /// <summary>
    /// Which roster seat this client is. -1 means "work it out from the character name".
    /// </summary>
    public int PinnedSlotIndex { get; set; } = -1;

    public ReminderChannel Channels { get; set; } =
        ReminderChannel.Overlay | ReminderChannel.Chat | ReminderChannel.Sound;

    /// <summary>Seconds the overlay banner stays up after firing.</summary>
    public float OverlayHoldSeconds { get; set; } = 4f;

    /// <summary>Scale applied to the overlay text on top of the global UI scale.</summary>
    public float OverlayTextScale { get; set; } = 2.0f;

    public Vector4 OverlayTextColor { get; set; } = new(1f, 0.86f, 0.4f, 1f);

    public Vector4 OverlayBackgroundColor { get; set; } = new(0f, 0f, 0f, 0.55f);

    /// <summary>Chat sound effect id (1-16) used when the Sound channel is on.</summary>
    public uint SoundEffectId { get; set; } = 6;

    /// <summary>
    /// Fallback line used when a timeline entry has no call text of its own.
    /// Supports the same tokens as per-entry text.
    /// </summary>
    public string DefaultTemplate { get; set; } = "{label}: {abilities}";

    /// <summary>Prefix stamped in front of every chat-channel call.</summary>
    public string ChatPrefix { get; set; } = "[RaidPlan]";

    /// <summary>Only deliver calls while in a duty.</summary>
    public bool OnlyInDuty { get; set; } = true;

    /// <summary>Show calls that are addressed to other players too, in a dimmer style.</summary>
    public bool ShowOtherPlayersCalls { get; set; }

    /// <summary>Extra seconds added to every entry's lead time for this client.</summary>
    public float LeadTimeAdjust { get; set; }

    public TeamProfile Clone()
    {
        return new TeamProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = Name + " (copy)",
            PinnedSlotIndex = PinnedSlotIndex,
            Channels = Channels,
            OverlayHoldSeconds = OverlayHoldSeconds,
            OverlayTextScale = OverlayTextScale,
            OverlayTextColor = OverlayTextColor,
            OverlayBackgroundColor = OverlayBackgroundColor,
            SoundEffectId = SoundEffectId,
            DefaultTemplate = DefaultTemplate,
            ChatPrefix = ChatPrefix,
            OnlyInDuty = OnlyInDuty,
            ShowOtherPlayersCalls = ShowOtherPlayersCalls,
            LeadTimeAdjust = LeadTimeAdjust,
        };
    }
}

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Id of the plan currently loaded in the planner window.</summary>
    public string ActivePlanId { get; set; } = string.Empty;

    public List<TeamProfile> Teams { get; set; } = new();

    public string ActiveTeamId { get; set; } = string.Empty;

    /// <summary>Master switch for live shotcalls.</summary>
    public bool RemindersEnabled { get; set; } = true;

    /// <summary>Open the planner automatically when a duty starts.</summary>
    public bool OpenOnDutyStart { get; set; }

    /// <summary>Follow the fight live: move the visible slide on as the pull progresses.</summary>
    public bool AutoAdvanceSlides { get; set; } = true;

    /// <summary>
    /// Switch slides the moment a boss cast a step is anchored to is detected, rather than
    /// waiting for that step's call to fire. This is the responsive option; turn it off if you
    /// would rather the slide arrive with the shotcall.
    /// </summary>
    public bool AutoAdvanceOnCast { get; set; } = true;

    /// <summary>Jump back to the first slide when a pull ends badly.</summary>
    public bool ResetSlidesOnWipe { get; set; } = true;

    /// <summary>
    /// How long automatic slide changes stand down after the player changes slides by hand.
    /// A wipe clears this immediately.
    /// </summary>
    public float ManualOverrideSeconds { get; set; } = 20f;

    /// <summary>Record fight timings across pulls and use them to predict mechanics.</summary>
    public bool LearningEnabled { get; set; } = true;

    /// <summary>
    /// How sure the plugin has to be of a learned timing before a predicted step will fire on it.
    /// Below this, the step waits for the real cast instead.
    /// </summary>
    public float MinimumPredictionConfidence { get; set; } = 0.45f;

    /// <summary>Position of the overlay banner, as a fraction of the main viewport.</summary>
    public Vector2 OverlayAnchor { get; set; } = new(0.5f, 0.28f);

    /// <summary>Let the overlay be dragged with the mouse.</summary>
    public bool OverlayUnlocked { get; set; }

    /// <summary>FF Logs API client id, from fflogs.com/api/clients. Only needed for log import.</summary>
    public string FfLogsClientId { get; set; } = string.Empty;

    public string FfLogsClientSecret { get; set; } = string.Empty;

    /// <summary>Last report someone imported, so the box is not empty next time.</summary>
    public string LastReportUrl { get; set; } = string.Empty;

    /// <summary>Show only cooldowns in the action picker, rather than a job's whole kit.</summary>
    public bool CooldownsOnly { get; set; } = true;

    /// <summary>Print every detected boss cast to the log window, for building timelines.</summary>
    public bool LogDetectedCasts { get; set; }

    // ---------------------------------------------------------------- mini plan

    /// <summary>When the compact in-fight window appears by itself.</summary>
    public MiniPlanVisibility MiniPlanMode { get; set; } = MiniPlanVisibility.RaidContent;

    /// <summary>Width of the mini plan in unscaled pixels. The game's minimap is around 218.</summary>
    public float MiniPlanSize { get; set; } = 220f;

    /// <summary>Where it sits, as a fraction of the viewport, so it survives a resolution change.</summary>
    public Vector2 MiniPlanAnchor { get; set; } = new(0.87f, 0.62f);

    /// <summary>Overall opacity of the panel behind the arena.</summary>
    public float MiniPlanOpacity { get; set; } = 0.85f;

    /// <summary>Ring the token belonging to this client's seat.</summary>
    public bool MiniPlanHighlightMe { get; set; } = true;

    /// <summary>Keep it draggable during a pull. Off by default so it can't eat a click.</summary>
    public bool MiniPlanUnlocked { get; set; }

    public TeamProfile GetActiveTeam()
    {
        if (Teams.Count == 0)
            Teams.Add(new TeamProfile());

        foreach (var team in Teams)
        {
            if (team.Id == ActiveTeamId)
                return team;
        }

        ActiveTeamId = Teams[0].Id;
        return Teams[0];
    }
}
