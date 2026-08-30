using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RaidPlan.Model;
using RaidPlan.Services;
using RaidPlan.UI.Theme;

namespace RaidPlan.UI;

public sealed partial class MainWindow
{

    private void DrawSlidesTab(RaidPlanDocument plan)
    {
        var avail = ImGui.GetContentRegionAvail();
        var spacing = ImGui.GetStyle().ItemSpacing.X * 2;
        var minCanvas = 200 * UiHelpers.Scale;

        var listWidth = Math.Clamp(avail.X * 0.17f, 120 * UiHelpers.Scale, 240 * UiHelpers.Scale);
        var inspectorWidth = Math.Clamp(avail.X * 0.25f, 150 * UiHelpers.Scale, 360 * UiHelpers.Scale);

        // Give the canvas its floor first, then whatever is left goes to the inspector.
        inspectorWidth = MathF.Min(inspectorWidth, avail.X - listWidth - minCanvas - spacing);
        var canvasWidth = avail.X - listWidth - inspectorWidth - spacing;

        // Too narrow for three panes: drop to the canvas alone and put the rest behind the tabs.
        if (inspectorWidth < 140 * UiHelpers.Scale || canvasWidth < minCanvas)
        {
            DrawCanvasPane(plan);
            return;
        }

        if (ImGui.BeginChild("##slide-list", new Vector2(listWidth, avail.Y), true, ImGuiWindowFlags.None))
            DrawSlideList(plan);
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("##slide-canvas", new Vector2(canvasWidth, avail.Y), false, ImGuiWindowFlags.None))
            DrawCanvasPane(plan);
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("##slide-inspector", new Vector2(inspectorWidth, avail.Y), true, ImGuiWindowFlags.None))
            DrawInspector(plan);
        ImGui.EndChild();
    }

    // ---------------------------------------------------------------- slide list

    private void DrawSlideList(RaidPlanDocument plan)
    {
        ImGui.TextDisabled("Slides");
        DrawFollowIndicator();
        ImGui.Separator();

        for (var i = 0; i < plan.Slides.Count; i++)
        {
            var slide = plan.Slides[i];
            ImGui.PushID("slide" + slide.Id);

            var label = $"{i + 1}. {slide.Title}";
            if (ImGui.Selectable(label, i == slideIndex, ImGuiSelectableFlags.None, Vector2.Zero))
                SelectSlideManually(i);

            if (ImGui.BeginPopupContextItem("##slide-ctx", ImGuiPopupFlags.MouseButtonRight))
            {
                if (ImGui.Selectable("Duplicate", false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    plan.Slides.Insert(i + 1, slide.Clone());
                    MarkDirty();
                }

                if (i > 0 && ImGui.Selectable("Move up", false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    (plan.Slides[i - 1], plan.Slides[i]) = (plan.Slides[i], plan.Slides[i - 1]);
                    slideIndex = i - 1;
                    MarkDirty();
                }

                if (i < plan.Slides.Count - 1 && ImGui.Selectable("Move down", false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    (plan.Slides[i + 1], plan.Slides[i]) = (plan.Slides[i], plan.Slides[i + 1]);
                    slideIndex = i + 1;
                    MarkDirty();
                }

                if (plan.Slides.Count > 1 && ImGui.Selectable("Delete", false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    plan.Slides.RemoveAt(i);
                    slideIndex = Math.Clamp(slideIndex, 0, plan.Slides.Count - 1);
                    canvas.Select(null);
                    MarkDirty();
                    ImGui.EndPopup();
                    ImGui.PopID();
                    return;
                }

                ImGui.EndPopup();
            }

            ImGui.PopID();
        }

        ImGui.Separator();

        if (ImGui.Button("Add slide", new Vector2(-1, 0)))
        {
            plan.Slides.Add(new Slide { Title = $"Slide {plan.Slides.Count + 1}" });
            slideIndex = plan.Slides.Count - 1;
            canvas.Select(null);
            MarkDirty();
        }

        if (plan.Slides.Count > 0 && ImGui.Button("Copy this slide", new Vector2(-1, 0)))
        {
            plan.Slides.Insert(slideIndex + 1, plan.Slides[slideIndex].Clone());
            slideIndex++;
            MarkDirty();
        }
    }

    /// <summary>
    /// A small badge saying whether the plan is currently driving itself, and why it last moved.
    /// Without this it is not obvious whether a slide changed on its own or got nudged.
    /// </summary>
    private void DrawFollowIndicator()
    {
        if (!Plugin.Config.AutoAdvanceSlides)
        {
            ImGui.TextDisabled("(manual)");
            if (ImGui.IsItemHovered())
                UiHelpers.Tooltip("Slides stay where you put them. Turn following on in settings, or with /raidplan follow.");
            return;
        }

        if (Plugin.Director.IsSuppressed)
        {
            ImGui.TextColored(
                UiHelpers.Pack(new Vector4(0.9f, 0.85f, 0.45f, 1f)),
                $"(paused {Plugin.Director.SuppressedFor:0}s)");

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("You changed slides by hand, so following is paused for a moment.");

            if (ImGui.SmallButton("Resume following"))
                Plugin.Director.ClearSuppression();

            return;
        }

        var recent = (DateTime.UtcNow - lastAutoChangeUtc).TotalSeconds < 6;
        var colour = recent ? new Vector4(0.5f, 0.9f, 0.5f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 1f);
        ImGui.TextColored(UiHelpers.Pack(colour), "(following)");

        if (!ImGui.IsItemHovered())
            return;

        var why = lastAutoChangeUtc == DateTime.MinValue
            ? "Nothing has moved the plan yet this session."
            : lastAutoChange switch
            {
                SlideChangeReason.CastDetected => "Last moved because a boss cast was detected.",
                SlideChangeReason.StepFired => "Last moved when a timeline step fired its call.",
                SlideChangeReason.CombatStarted => "Last reset because a pull started.",
                SlideChangeReason.Wipe => "Last reset because of a wipe.",
                _ => "Last moved by hand.",
            };

        ImGui.SetTooltip("Slides follow the fight.\n" + why);
    }

    // ---------------------------------------------------------------- canvas pane

    private void DrawCanvasPane(RaidPlanDocument plan)
    {
        var slide = CurrentSlide;
        if (slide == null)
        {
            ImGui.TextDisabled("This plan has no slides yet.");
            return;
        }

        ImGui.SetNextItemWidth(-1);
        var title = slide.Title;
        if (UiHelpers.InputTextHint("##slide-title", "Slide title", ref title, 128))
        {
            slide.Title = title;
            MarkDirty();
        }

        DrawToolbar(plan);

        ImGui.Spacing();

        var avail = ImGui.GetContentRegionAvail();
        var notesHeight = ImGui.GetTextLineHeightWithSpacing() + (70 * UiHelpers.Scale) +
                          (ImGui.GetStyle().ItemSpacing.Y * 3);
        var canvasArea = new Vector2(avail.X, MathF.Max(120 * UiHelpers.Scale, avail.Y - notesHeight));

        if (canvas.Draw(plan, slide, canvasArea, editable: true))
            MarkDirty();

        ImGui.Spacing();
        ImGui.TextDisabled("Notes");
        var notes = slide.Notes;
        if (UiHelpers.InputMultiline("##slide-notes", ref notes, new Vector2(-1, 70 * UiHelpers.Scale)))
        {
            slide.Notes = notes;
            MarkDirty();
        }
    }

    private void DrawToolbar(RaidPlanDocument plan)
    {
        var useIcons = Plugin.Config.ThemeToolIcons;
        var square = new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());

        for (var i = 0; i < ToolCatalog.All.Count; i++)
        {
            var entry = ToolCatalog.All[i];

            if (i > 0)
                UiHelpers.SameLineIfRoom(useIcons ? square.X : UiHelpers.ButtonWidth(entry.Label));

            var isActive = canvas.Tool == entry.Tool;
            if (isActive)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Palette.Vec(Palette.Accent, 0.26f));
                ImGui.PushStyleColor(ImGuiCol.Border, Palette.Vec(Palette.Accent, 0.9f));
            }

            var pressed = false;
            var drewIcon = useIcons && ThemeFonts.TryIconButton(entry.Icon, "tool" + i, square, out pressed);

            // No icon font, or the player asked for words: a plain labelled button.
            if (!drewIcon)
                pressed = ImGui.Button(entry.Label + "##tool" + i, Vector2.Zero);

            if (pressed)
                canvas.Tool = entry.Tool;

            if (isActive)
                ImGui.PopStyleColor(2);

            if (ImGui.IsItemHovered())
                UiHelpers.Tooltip(entry.Label, entry.Tip);
        }

        // Contextual options for the selected tool.
        switch (canvas.Tool)
        {
            case CanvasTool.PlayerToken:
            {
                ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
                var slot = Math.Clamp(canvas.BrushSlot, 0, Math.Max(0, plan.Roster.Count - 1));
                var preview = plan.Roster.Count > 0 ? SeatLabel(plan, slot) : "no seats";
                if (ImGui.BeginCombo("Seat##brush-slot", preview, ImGuiComboFlags.None))
                {
                    for (var i = 0; i < plan.Roster.Count; i++)
                    {
                        if (ImGui.Selectable(SeatLabel(plan, i) + "##seat" + i, i == slot, ImGuiSelectableFlags.None, Vector2.Zero))
                            canvas.BrushSlot = i;
                    }

                    ImGui.EndCombo();
                }

                break;
            }

            case CanvasTool.Waymark:
            {
                var marks = new[] { "A", "B", "C", "D", "1", "2", "3", "4" };
                var markWidth = 26 * UiHelpers.Scale;

                for (var m = 0; m < marks.Length; m++)
                {
                    if (m > 0)
                        UiHelpers.SameLineIfRoom(markWidth);

                    var active = canvas.BrushWaymark == marks[m];
                    if (active)
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.26f, 0.45f, 0.72f, 1f));
                    if (ImGui.Button(marks[m] + "##wm", new Vector2(markWidth, 0)))
                        canvas.BrushWaymark = marks[m];
                    if (active)
                        ImGui.PopStyleColor();
                }

                break;
            }

            case CanvasTool.Zone:
            {
                ImGui.SetNextItemWidth(150 * UiHelpers.Scale);
                if (ImGui.BeginCombo("Shape##brush-zone", canvas.BrushZone.ToString(), ImGuiComboFlags.None))
                {
                    foreach (ZoneShape shape in Enum.GetValues<ZoneShape>())
                    {
                        if (ImGui.Selectable(shape.ToString(), shape == canvas.BrushZone, ImGuiSelectableFlags.None, Vector2.Zero))
                            canvas.BrushZone = shape;
                    }

                    ImGui.EndCombo();
                }

                break;
            }
        }

        if (canvas.Tool is not (CanvasTool.Select or CanvasTool.PlayerToken))
        {
            if (canvas.Tool == CanvasTool.Zone)
                ImGui.SameLine();

            var colour = canvas.BrushColor;
            if (UiHelpers.ColorButton("Colour##brush", ref colour, "Colour used for new items"))
                canvas.BrushColor = colour;
        }
    }

    private static string SeatLabel(RaidPlanDocument plan, int index)
    {
        if (index < 0 || index >= plan.Roster.Count)
            return "—";

        var slot = plan.Roster[index];
        var job = slot.JobId != 0 ? " " + Plugin.Actions.JobAbbreviation(slot.JobId) : string.Empty;
        var name = string.IsNullOrWhiteSpace(slot.Name) ? string.Empty : " · " + slot.Name;
        return $"{index + 1}. {slot.DisplayName}{job}{name}";
    }

    // ---------------------------------------------------------------- inspector

    private void DrawInspector(RaidPlanDocument plan)
    {
        var slide = CurrentSlide;
        if (slide == null)
            return;

        var item = canvas.GetSelected(slide);
        if (item == null)
        {
            DrawArenaSettings(plan);
            return;
        }

        ImGui.TextDisabled($"Selected: {item.Kind}");
        ImGui.Separator();

        if (item.Kind is CanvasItemKind.Label or CanvasItemKind.EnemyToken or CanvasItemKind.PlayerToken)
        {
            ImGui.SetNextItemWidth(-1);
            var text = item.Text;
            if (UiHelpers.InputTextHint("##item-text", "Caption", ref text, 64))
            {
                item.Text = text;
                MarkDirty();
            }
        }

        if (item.Kind == CanvasItemKind.PlayerToken)
        {
            ImGui.SetNextItemWidth(-1);
            var preview = item.SlotIndex >= 0 ? SeatLabel(plan, item.SlotIndex) : "unbound";
            if (ImGui.BeginCombo("##item-slot", preview, ImGuiComboFlags.None))
            {
                if (ImGui.Selectable("unbound", item.SlotIndex < 0, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    item.SlotIndex = -1;
                    MarkDirty();
                }

                for (var i = 0; i < plan.Roster.Count; i++)
                {
                    if (ImGui.Selectable(SeatLabel(plan, i) + "##islot" + i, item.SlotIndex == i, ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        item.SlotIndex = i;
                        MarkDirty();
                    }
                }

                ImGui.EndCombo();
            }
        }

        if (item.Kind == CanvasItemKind.Waymark)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##item-waymark", string.IsNullOrEmpty(item.Text) ? "A" : item.Text, ImGuiComboFlags.None))
            {
                foreach (var mark in new[] { "A", "B", "C", "D", "1", "2", "3", "4" })
                {
                    if (ImGui.Selectable(mark, item.Text == mark, ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        item.Text = mark;
                        MarkDirty();
                    }
                }

                ImGui.EndCombo();
            }
        }

        if (item.Kind == CanvasItemKind.Zone)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##item-zone", item.Zone.ToString(), ImGuiComboFlags.None))
            {
                foreach (var shape in Enum.GetValues<ZoneShape>())
                {
                    if (ImGui.Selectable(shape.ToString(), shape == item.Zone, ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        item.Zone = shape;
                        MarkDirty();
                    }
                }

                ImGui.EndCombo();
            }
        }

        var colour = item.Color;
        if (UiHelpers.ColorButton("Colour##item", ref colour))
        {
            item.Color = colour;
            MarkDirty();
        }

        if (item.Kind is CanvasItemKind.PlayerToken or CanvasItemKind.EnemyToken or CanvasItemKind.Waymark)
        {
            var radius = item.Radius;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("Size##item", ref radius, 0.02f, 0.2f, "%.3f", ImGuiSliderFlags.None))
            {
                item.Radius = radius;
                MarkDirty();
            }
        }

        if (item.Kind == CanvasItemKind.Zone)
        {
            var radius = item.Radius;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("Radius / length##item", ref radius, 0.02f, 0.8f, "%.3f", ImGuiSliderFlags.None))
            {
                item.Radius = radius;
                MarkDirty();
            }

            if (item.Zone == ZoneShape.Donut)
            {
                var inner = item.InnerRadius;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("Inner radius##item", ref inner, 0.01f, 0.7f, "%.3f", ImGuiSliderFlags.None))
                {
                    item.InnerRadius = inner;
                    MarkDirty();
                }
            }

            if (item.Zone == ZoneShape.Cone)
            {
                var angle = item.ConeAngle;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("Sweep°##item", ref angle, 10f, 350f, "%.0f", ImGuiSliderFlags.None))
                {
                    item.ConeAngle = angle;
                    MarkDirty();
                }
            }

            if (item.Zone is ZoneShape.Rectangle or ZoneShape.Line or ZoneShape.Cross)
            {
                var width = item.Extent.X;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat("Width##item", ref width, 0.01f, 0.5f, "%.3f", ImGuiSliderFlags.None))
                {
                    item.Extent = new Vector2(width, item.Extent.Y);
                    MarkDirty();
                }

                if (item.Zone == ZoneShape.Rectangle)
                {
                    var height = item.Extent.Y;
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.SliderFloat("Height##item", ref height, 0.01f, 0.5f, "%.3f", ImGuiSliderFlags.None))
                    {
                        item.Extent = new Vector2(item.Extent.X, height);
                        MarkDirty();
                    }
                }
            }
        }

        if (item.Kind is CanvasItemKind.Zone or CanvasItemKind.Label)
        {
            var rotation = item.Rotation;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("Facing°##item", ref rotation, 0f, 360f, "%.0f", ImGuiSliderFlags.None))
            {
                item.Rotation = rotation;
                MarkDirty();
            }
        }

        if (item.Kind is CanvasItemKind.Arrow or CanvasItemKind.Tether or CanvasItemKind.Freehand)
        {
            var thickness = item.Thickness;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("Thickness##item", ref thickness, 0.002f, 0.03f, "%.4f", ImGuiSliderFlags.None))
            {
                item.Thickness = thickness;
                MarkDirty();
            }
        }

        var locked = item.Locked;
        if (ImGui.Checkbox("Locked", ref locked))
        {
            item.Locked = locked;
            MarkDirty();
        }

        ImGui.SameLine();
        var layer = item.Layer;
        ImGui.SetNextItemWidth(80 * UiHelpers.Scale);
        if (ImGui.InputInt("Layer", ref layer, 1, 1, "%d", ImGuiInputTextFlags.None))
        {
            item.Layer = layer;
            MarkDirty();
        }

        ImGui.Separator();

        if (ImGui.Button("Duplicate", new Vector2(-1, 0)))
        {
            var copy = item.Clone();
            copy.Position += new Vector2(0.04f, 0.04f);
            for (var i = 0; i < copy.Points.Count; i++)
                copy.Points[i] += new Vector2(0.04f, 0.04f);
            slide.Items.Add(copy);
            canvas.Select(copy.Id);
            MarkDirty();
        }

        if (ImGui.Button("Delete", new Vector2(-1, 0)))
        {
            slide.Items.Remove(item);
            canvas.Select(null);
            MarkDirty();
        }
    }

    private void DrawArenaSettings(RaidPlanDocument plan)
    {
        ImGui.TextDisabled("Arena");
        ImGui.Separator();
        ImGui.TextWrapped("Nothing is selected. Click something on the arena to edit it, or set up the arena itself here.");
        ImGui.Spacing();

        var arena = plan.Arena;

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##arena-shape", arena.Shape.ToString(), ImGuiComboFlags.None))
        {
            foreach (var shape in Enum.GetValues<ArenaShape>())
            {
                if (ImGui.Selectable(shape.ToString(), shape == arena.Shape, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    arena.Shape = shape;
                    MarkDirty();
                }
            }

            ImGui.EndCombo();
        }

        if (arena.Shape == ArenaShape.Rectangle)
        {
            var ratio = arena.AspectRatio;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("Width : height", ref ratio, 0.3f, 3f, "%.2f", ImGuiSliderFlags.None))
            {
                arena.AspectRatio = ratio;
                MarkDirty();
            }
        }

        var grid = arena.ShowGrid;
        if (ImGui.Checkbox("Grid", ref grid))
        {
            arena.ShowGrid = grid;
            MarkDirty();
        }

        if (arena.ShowGrid)
        {
            var divisions = arena.GridDivisions;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderInt("Divisions", ref divisions, 2, 16, "%d", ImGuiSliderFlags.None))
            {
                arena.GridDivisions = divisions;
                MarkDirty();
            }
        }

        var cardinals = arena.ShowCardinals;
        if (ImGui.Checkbox("Compass letters", ref cardinals))
        {
            arena.ShowCardinals = cardinals;
            MarkDirty();
        }

        var guides = arena.ShowWaymarkGuides;
        if (ImGui.Checkbox("Waymark guides", ref guides))
        {
            arena.ShowWaymarkGuides = guides;
            MarkDirty();
        }

        var background = arena.BackgroundColor;
        if (UiHelpers.ColorButton("Floor##arena", ref background))
        {
            arena.BackgroundColor = background;
            MarkDirty();
        }

        var line = arena.LineColor;
        if (UiHelpers.ColorButton("Outline##arena", ref line))
        {
            arena.LineColor = line;
            MarkDirty();
        }

        var gridColour = arena.GridColor;
        if (UiHelpers.ColorButton("Grid##arena", ref gridColour))
        {
            arena.GridColor = gridColour;
            MarkDirty();
        }

        ImGui.Separator();
        var slide = CurrentSlide;
        if (slide != null && slide.Items.Count > 0 && ImGui.Button("Clear this slide", new Vector2(-1, 0)))
        {
            slide.Items.Clear();
            canvas.Select(null);
            MarkDirty();
        }
    }
}
