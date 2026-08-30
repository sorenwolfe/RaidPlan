using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using RaidPlan.Model;
using RaidPlan.Services;
using RaidPlan.UI.Theme;

namespace RaidPlan.UI;

/// <summary>
/// A minimap-sized copy of the current slide, for reading mid-pull. It puts itself on screen in
/// raid content and gets out of the way everywhere else.
///
/// During a pull it ignores the mouse completely, so a click meant for the game can never land
/// here instead. Out of combat it takes the mouse back, which is when you drag it somewhere else
/// or close it.
/// </summary>
public sealed class MiniPlanWindow : Window, IDisposable
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoDecoration |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoBackground |
        ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse;

    private readonly ArenaCanvas canvas = new();
    private readonly ZoneClassifier zone = new();

    private bool ignoringMouse = true;
    private bool anchorPendingSave;
    private bool dragging;
    private Vector2 dragGrab;
    private ThemeScope theme;

    public MiniPlanWindow()
        : base("##raidplan-mini", BaseFlags)
    {
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        AllowPinning = false;
        AllowClickthrough = false;
        ForceMainWindow = true;
        IsOpen = true;
    }

    /// <summary>Set by /raidplan mini. Lets the window be summoned outside raid content.</summary>
    public bool ManuallyOpen { get; private set; }

    /// <summary>
    /// Not named Toggle: the base Window.Toggle flips IsOpen, which this window ignores in
    /// favour of DrawConditions. Hiding it would be a quiet trap for whoever calls it next.
    /// </summary>
    public void ToggleShown()
    {
        // Closing while it is showing on its own should actually close it, so the toggle
        // reflects what is on screen rather than only the manual flag.
        if (!ManuallyOpen && Visible())
        {
            Plugin.Config.MiniPlanMode = MiniPlanVisibility.Never;
            ManuallyOpen = false;
        }
        else
        {
            ManuallyOpen = !ManuallyOpen;
        }

        Plugin.SaveConfig();
    }

    public void Close()
    {
        ManuallyOpen = false;
        Plugin.Config.MiniPlanMode = MiniPlanVisibility.Never;
        Plugin.SaveConfig();
    }

    private bool Visible()
    {
        zone.Refresh();

        return ContentPolicy.ShouldShow(
            Plugin.Config.MiniPlanMode,
            ManuallyOpen,
            Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty],
            zone.ContentTypeId,
            zone.HighEndDuty,
            Plugin.Encounter.InCombat,
            Plugin.Plans.Active is { Slides.Count: > 0 });
    }

    public override bool DrawConditions() => Visible();

    public override void PreDraw()
    {
        ignoringMouse = ContentPolicy.ShouldIgnoreMouse(
            Plugin.Encounter.InCombat, Plugin.Config.MiniPlanUnlocked);

        Flags = ignoringMouse ? BaseFlags | ImGuiWindowFlags.NoInputs : BaseFlags;

        if (ignoringMouse)
            dragging = false;

        var side = Math.Clamp(Plugin.Config.MiniPlanSize, 120f, 520f) * UiHelpers.Scale;
        ImGui.SetNextWindowSize(new Vector2(side, side), ImGuiCond.Always);

        // Only steer the position while the mouse is off it. Otherwise a drag fights the anchor.
        if (ignoringMouse)
        {
            var viewport = ImGuiHelpers.MainViewport;
            var position = viewport.Pos + (viewport.Size * Plugin.Config.MiniPlanAnchor);
            ImGui.SetNextWindowPos(position, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        }

        // Theme first, then the padding, so the pops below unwind in the reverse order.
        theme = ThemeScope.Push();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
        theme.Dispose();

        if (ignoringMouse)
            return;

        RememberPosition();
    }

    public override void Draw()
    {
        var plan = Plugin.Plans.Active;
        if (plan == null || plan.Slides.Count == 0)
            return;

        // Mirrors the planner rather than tracking its own position, so auto-advance, /raidplan
        // next and a wipe reset all move both at once.
        var index = Math.Clamp(Plugin.Main.SlideIndex, 0, plan.Slides.Count - 1);
        var slide = plan.Slides[index];

        var min = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var drawList = ImGui.GetWindowDrawList();

        DrawPanel(drawList, min, size);

        canvas.HighlightSlot = Plugin.Config.MiniPlanHighlightMe
            ? Plugin.Roster.ResolveLocalSlot(plan)
            : -1;

        // The arena is square and the panel has a hairline inset, so give the canvas the inner box.
        var inset = 3f * UiHelpers.Scale;
        ImGui.SetCursorPos(new Vector2(inset, inset));
        canvas.Draw(plan, slide, new Vector2(size.X - (inset * 2), size.Y - (inset * 2)), editable: false);

        DrawSlideDots(drawList, min, size, plan.Slides.Count, index);

        if (!ignoringMouse)
            DrawIdleChrome(drawList, min, size);
    }

    /// <summary>The panel behind the arena: rounded, translucent, one hairline border.</summary>
    private static void DrawPanel(ImDrawListPtr drawList, Vector2 min, Vector2 size)
    {
        var max = min + size;
        var alpha = Math.Clamp(Plugin.Config.MiniPlanOpacity, 0.1f, 1f);
        var rounding = 8f * UiHelpers.Scale;

        if (Plugin.Config.ThemeShadows)
            Sprites.Shadow(drawList, min, max, 14f * UiHelpers.Scale, 0.55f * alpha);

        drawList.AddRectFilled(min, max, Palette.Pack(Palette.Window, alpha), rounding);
        drawList.AddRect(min, max, Palette.Line(0.14f), rounding, ImDrawFlags.None, 1f);
    }

    /// <summary>
    /// One dot per slide along the bottom edge, so you can see the plan moving under you without
    /// spending room on a title.
    /// </summary>
    private static void DrawSlideDots(ImDrawListPtr drawList, Vector2 min, Vector2 size, int count, int current)
    {
        if (count < 2 || count > 24)
            return;

        var radius = 1.9f * UiHelpers.Scale;
        var gap = radius * 3.2f;
        var y = min.Y + size.Y - (7f * UiHelpers.Scale);
        var startX = min.X + (size.X * 0.5f) - ((count - 1) * gap * 0.5f);

        for (var i = 0; i < count; i++)
        {
            var here = i == current;
            drawList.AddCircleFilled(
                new Vector2(startX + (i * gap), y),
                here ? radius * 1.5f : radius,
                UiHelpers.WithAlpha(0xFFFFFFFF, here ? 0.85f : 0.25f),
                12);
        }
    }

    /// <summary>
    /// Shown only when the window is taking the mouse — that is, out of combat. The close button
    /// and the drag are hit-tested by hand rather than left to ImGui: this window has no title
    /// bar, and ImGui's drag-anywhere fallback is something the player can switch off in Dalamud's
    /// settings, which would silently leave the thing stuck where it is.
    /// </summary>
    private void DrawIdleChrome(ImDrawListPtr drawList, Vector2 min, Vector2 size)
    {
        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var mouse = ImGui.GetMousePos();
        var max = min + size;

        var button = 15f * UiHelpers.Scale;
        var pad = 6f * UiHelpers.Scale;
        var closeMin = new Vector2(max.X - pad - button, min.Y + pad);
        var closeMax = closeMin + new Vector2(button, button);

        var overClose = hovered &&
                        mouse.X >= closeMin.X && mouse.X <= closeMax.X &&
                        mouse.Y >= closeMin.Y && mouse.Y <= closeMax.Y;

        if (!dragging && hovered && !overClose && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragGrab = mouse - min;
        }

        if (dragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                ImGui.SetWindowPos(mouse - dragGrab, ImGuiCond.Always);
            else
                dragging = false;
        }

        if (!hovered && !dragging)
            return;

        var rounding = 8f * UiHelpers.Scale;
        drawList.AddRect(min, max, UiHelpers.WithAlpha(0xFFFFFFFF, 0.30f), rounding, ImDrawFlags.None, 1.5f);

        var centre = closeMin + new Vector2(button * 0.5f, button * 0.5f);
        drawList.AddCircleFilled(centre, button * 0.5f,
            UiHelpers.WithAlpha(overClose ? 0xFF3B4CE0 : 0xFF000000, overClose ? 0.9f : 0.45f), 16);

        var arm = button * 0.22f;
        var cross = UiHelpers.WithAlpha(0xFFFFFFFF, overClose ? 1f : 0.7f);
        drawList.AddLine(centre - new Vector2(arm, arm), centre + new Vector2(arm, arm), cross, 1.6f);
        drawList.AddLine(centre - new Vector2(arm, -arm), centre + new Vector2(arm, -arm), cross, 1.6f);

        // Released rather than clicked, so a click that started elsewhere and drifted over the
        // button on the way up doesn't close the window mid-drag.
        if (overClose && !dragging && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            Close();
            return;
        }

        if (dragging)
            return;

        if (overClose)
            UiHelpers.Tooltip("Hide the mini plan. Bring it back with /raidplan mini.");
        else
            UiHelpers.Tooltip("Drag to move. It stops taking clicks once the pull starts.");
    }

    private void RememberPosition()
    {
        var viewport = ImGuiHelpers.MainViewport;
        if (viewport.Size.X <= 0 || viewport.Size.Y <= 0)
            return;

        var centre = ImGui.GetWindowPos() + (ImGui.GetWindowSize() * 0.5f);
        var anchor = (centre - viewport.Pos) / viewport.Size;

        if (Vector2.Distance(anchor, Plugin.Config.MiniPlanAnchor) > 0.001f)
        {
            Plugin.Config.MiniPlanAnchor = anchor;
            anchorPendingSave = true;
        }

        // Written once the drag ends, so moving it isn't a hundred config writes.
        if (anchorPendingSave && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            anchorPendingSave = false;
            Plugin.SaveConfig();
        }
    }

    public void Dispose()
    {
    }
}
