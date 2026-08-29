using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace RaidPlan.UI;

/// <summary>
/// The shotcall banner. It has no chrome and ignores the mouse unless the user has unlocked it,
/// so it can sit over the middle of the screen without getting in the way of a pull.
/// </summary>
public sealed class OverlayWindow : Window, IDisposable
{
    private const ImGuiWindowFlags LockedFlags =
        ImGuiWindowFlags.NoDecoration |
        ImGuiWindowFlags.NoInputs |
        ImGuiWindowFlags.NoBackground |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.AlwaysAutoResize;

    private const ImGuiWindowFlags UnlockedFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.AlwaysAutoResize;

    public OverlayWindow()
        : base("##raidplan-overlay", LockedFlags)
    {
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        AllowPinning = false;
        ForceMainWindow = true;
    }

    public override bool DrawConditions()
    {
        if (Plugin.Config.OverlayUnlocked)
            return true;

        return Plugin.Reminders.ActiveCalls.Count > 0;
    }

    public override void PreDraw()
    {
        Flags = Plugin.Config.OverlayUnlocked ? UnlockedFlags : LockedFlags;

        if (Plugin.Config.OverlayUnlocked)
            return;

        var viewport = ImGuiHelpers.MainViewport;
        var anchor = Plugin.Config.OverlayAnchor;
        var position = viewport.Pos + (viewport.Size * anchor);
        ImGui.SetNextWindowPos(position, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
    }

    private bool anchorPendingSave;

    public override void PostDraw()
    {
        if (!Plugin.Config.OverlayUnlocked)
            return;

        // While unlocked the user drags the window; remember where they put it. The position is
        // tracked every frame but only written to disk once the mouse comes up, so a slow drag
        // does not turn into a hundred config writes.
        var viewport = ImGuiHelpers.MainViewport;
        if (viewport.Size.X <= 0 || viewport.Size.Y <= 0)
            return;

        var centre = ImGui.GetWindowPos() + (ImGui.GetWindowSize() * 0.5f);
        var anchor = (centre - viewport.Pos) / viewport.Size;

        if (Vector2.Distance(anchor, Plugin.Config.OverlayAnchor) > 0.001f)
        {
            Plugin.Config.OverlayAnchor = anchor;
            anchorPendingSave = true;
        }

        if (anchorPendingSave && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            anchorPendingSave = false;
            Plugin.SaveConfig();
        }
    }

    public override void Draw()
    {
        var team = Plugin.Config.GetActiveTeam();
        var calls = Plugin.Reminders.ActiveCalls;

        if (Plugin.Config.OverlayUnlocked && calls.Count == 0)
        {
            ImGui.TextUnformatted("RaidPlan banner — drag me, then lock in settings.");
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = Math.Clamp(team.OverlayTextScale, 0.5f, 6f);

        ImGui.SetWindowFontScale(scale);

        for (var i = 0; i < calls.Count; i++)
        {
            var call = calls[i];

            // Older calls sit underneath and fade out.
            var age = (float)(DateTime.UtcNow - call.FiredAtUtc).TotalSeconds;
            var life = MathF.Max(0.5f, team.OverlayHoldSeconds);
            var fade = Math.Clamp(1f - ((age - (life * 0.6f)) / (life * 0.4f)), 0f, 1f);
            var dim = i == 0 ? 1f : 0.6f;
            var alpha = fade * dim * (call.ForLocalPlayer ? 1f : 0.65f);

            if (alpha <= 0.01f)
                continue;

            var textColour = team.OverlayTextColor;
            textColour.W *= alpha;

            var start = ImGui.GetCursorScreenPos();
            var size = UiHelpers.TextSize(call.Headline);

            var pad = new Vector2(10f, 5f) * UiHelpers.Scale * scale;
            var back = team.OverlayBackgroundColor;
            back.W *= alpha;

            drawList.AddRectFilled(start - pad, start + size + pad, UiHelpers.Pack(back), 5f * scale);

            // A slim accent stripe in the seat's colour, so a glance is enough.
            drawList.AddRectFilled(
                start - pad,
                new Vector2(start.X - pad.X + (3f * scale), start.Y + size.Y + pad.Y),
                UiHelpers.WithAlpha(call.AccentColor, alpha),
                5f * scale);

            ImGui.TextColored(UiHelpers.Pack(textColour), call.Headline);

            if (!string.IsNullOrWhiteSpace(call.SubLine) && i == 0)
            {
                ImGui.SetWindowFontScale(scale * 0.55f);
                var sub = team.OverlayTextColor;
                sub.W *= alpha * 0.75f;
                ImGui.TextColored(UiHelpers.Pack(sub), call.SubLine);
                ImGui.SetWindowFontScale(scale);
            }

            ImGui.Dummy(new Vector2(0, 4f * UiHelpers.Scale));
        }

        ImGui.SetWindowFontScale(1f);
    }

    public void Dispose()
    {
    }
}
