using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RaidPlan.Model;
using RaidPlan.Services;

namespace RaidPlan.UI;

public sealed partial class MainWindow
{
    private readonly SpellPicker castPicker = new();
    private readonly SpellPicker assignPicker = new();

    private string? selectedEntryId;
    private string timeBuffer = string.Empty;
    private string timeBufferFor = string.Empty;

    private void DrawTimelineTab(RaidPlanDocument plan)
    {
        var avail = ImGui.GetContentRegionAvail();
        var listWidth = Math.Clamp(avail.X * 0.3f, 220 * UiHelpers.Scale, 380 * UiHelpers.Scale);

        if (ImGui.BeginChild("##timeline-list", new Vector2(listWidth, avail.Y), true, ImGuiWindowFlags.None))
            DrawTimelineList(plan);
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("##timeline-editor", new Vector2(0, avail.Y), true, ImGuiWindowFlags.None))
        {
            var entry = plan.Timeline.FirstOrDefault(e => e.Id == selectedEntryId);
            if (entry == null)
            {
                ImGui.TextWrapped(
                    "Pick a step on the left, or add one.\n\n" +
                    "A step is a moment in the fight: when it happens, which slide explains it, " +
                    "what each player presses, and what they get told a few seconds beforehand.");
            }
            else
            {
                DrawEntryEditor(plan, entry);
            }
        }

        ImGui.EndChild();
    }

    private void DrawTimelineList(RaidPlanDocument plan)
    {
        ImGui.TextDisabled("Fight timeline");
        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Steps run in the order shown. The time column is for your own reading — what actually " +
            "fires a call is the trigger you set on each step.");

        ImGui.Separator();

        var ordered = plan.Timeline.OrderBy(e => e.SortTime).ThenBy(e => e.Label, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var entry in ordered)
        {
            ImGui.PushID("tl" + entry.Id);

            var enabled = entry.Enabled;
            if (ImGui.Checkbox("##enabled", ref enabled))
            {
                entry.Enabled = enabled;
                MarkDirty();
            }

            ImGui.SameLine();

            var trigger = entry.Trigger switch
            {
                TriggerKind.BossCast => "cast",
                TriggerKind.AfterCast => "after",
                TriggerKind.CombatTime => "clock",
                TriggerKind.Predicted => "learned",
                _ => "manual",
            };

            var tag = $"[{trigger}]";
            var tagWidth = UiHelpers.TextSize(tag).X;
            var regionWidth = ImGui.GetContentRegionAvail().X;

            var label = $"{CallTemplate.FormatTime(entry.SortTime)}  {entry.Label}";
            if (ImGui.Selectable(label, entry.Id == selectedEntryId, ImGuiSelectableFlags.None, Vector2.Zero))
                selectedEntryId = entry.Id;

            // Bind the context menu to the row, before the tag becomes the last item.
            var rowHovered = ImGui.IsItemHovered();
            if (rowHovered && !string.IsNullOrEmpty(entry.Label))
                ImGui.SetTooltip(entry.Label);

            if (ImGui.BeginPopupContextItem("##tl-ctx", ImGuiPopupFlags.MouseButtonRight))
            {
                if (ImGui.Selectable("Duplicate", false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    var copy = entry.Clone();
                    plan.Timeline.Add(copy);
                    selectedEntryId = copy.Id;
                    MarkDirty();
                }

                if (ImGui.Selectable("Test this call", false, ImGuiSelectableFlags.None, Vector2.Zero))
                    Plugin.Reminders.FireNow(plan, entry);

                if (ImGui.Selectable("Delete", false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    plan.Timeline.Remove(entry);
                    if (selectedEntryId == entry.Id)
                        selectedEntryId = null;
                    MarkDirty();
                }

                ImGui.EndPopup();
            }

            ImGui.SameLine(MathF.Max(0f, regionWidth - tagWidth));
            ImGui.TextDisabled(tag);

            ImGui.PopID();
        }

        ImGui.Separator();

        if (ImGui.Button("Add step", new Vector2(-1, 0)))
        {
            var entry = new TimelineEntry
            {
                Label = "New step",
                SortTime = plan.Timeline.Count > 0 ? plan.Timeline.Max(e => e.SortTime) + 30f : 30f,
            };
            plan.Timeline.Add(entry);
            selectedEntryId = entry.Id;
            MarkDirty();
        }

        if (Plugin.Encounter.RecentCasts.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled("Seen this pull — click to add");

            foreach (var cast in Plugin.Encounter.RecentCasts.AsEnumerable().Reverse().Take(12))
            {
                ImGui.PushID("cast" + cast.ActionId + "-" + cast.Occurrence);
                var label = $"{CallTemplate.FormatTime(cast.CombatTime)}  {cast.ActionName} ×{cast.Occurrence}";
                if (ImGui.Selectable(label, false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    var entry = new TimelineEntry
                    {
                        Label = cast.ActionName,
                        Trigger = TriggerKind.BossCast,
                        CastActionId = cast.ActionId,
                        CastName = cast.ActionName,
                        Occurrence = cast.Occurrence,
                        SortTime = cast.CombatTime,
                        LeadSeconds = Math.Clamp(cast.TotalCastTime - 1f, 1f, 10f),
                    };
                    plan.Timeline.Add(entry);
                    selectedEntryId = entry.Id;
                    MarkDirty();
                }

                ImGui.PopID();
            }
        }
    }

    private void DrawEntryEditor(RaidPlanDocument plan, TimelineEntry entry)
    {
        ImGui.SetNextItemWidth(-1);
        var label = entry.Label;
        if (UiHelpers.InputTextHint("##entry-label", "Step name", ref label, 128))
        {
            entry.Label = label;
            MarkDirty();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("When");
        ImGui.Separator();

        ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
        if (ImGui.BeginCombo("Trigger", TriggerLabel(entry.Trigger), ImGuiComboFlags.None))
        {
            foreach (var kind in Enum.GetValues<TriggerKind>())
            {
                if (ImGui.Selectable(TriggerLabel(kind), kind == entry.Trigger, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    entry.Trigger = kind;
                    MarkDirty();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Boss cast — the plugin watches for the boss actually starting that cast, then calls it " +
            "with your lead time to spare. Survives phase pushes and slow pulls, but cannot warn " +
            "you earlier than the cast bar is long.\n\n" +
            "Learned timing — fires when RaidPlan expects the cast, based on your previous pulls, " +
            "corrected for how fast this one is running. This is the only trigger that can warn " +
            "before the boss commits. Falls back to Boss cast behaviour until it has watched the " +
            "fight enough times to be sure.\n\n" +
            "After a cast — anchor to a cast, then fire a fixed number of seconds later. Use this to " +
            "pre-warn a mechanic that has no cast bar of its own.\n\n" +
            "Combat clock — a plain stopwatch from the pull.\n\n" +
            "Manual — never fires on its own; only from the Test button.");

        if (entry.Trigger is TriggerKind.BossCast or TriggerKind.AfterCast or TriggerKind.Predicted)
        {
            ImGui.TextUnformatted("Boss cast");
            ImGui.SameLine(160 * UiHelpers.Scale);
            var castId = entry.CastActionId;
            var castName = entry.CastName;
            if (castPicker.Draw("##entry-cast", 0, ref castId, ref castName, playerActionsOnly: false))
            {
                entry.CastActionId = castId;
                entry.CastName = castName;
                if (entry.Label is "New step" or "" && !string.IsNullOrEmpty(castName))
                    entry.Label = castName;
                MarkDirty();
            }

            ImGui.TextUnformatted("Which use");
            ImGui.SameLine(160 * UiHelpers.Scale);
            ImGui.SetNextItemWidth(120 * UiHelpers.Scale);
            var occurrence = entry.Occurrence;
            if (ImGui.InputInt("##entry-occurrence", ref occurrence, 1, 1, "%d", ImGuiInputTextFlags.None))
            {
                entry.Occurrence = occurrence;
                MarkDirty();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(entry.Occurrence <= 0 ? "every time" : Ordinal(entry.Occurrence) + " use this pull");
        }

        if (entry.Trigger == TriggerKind.AfterCast)
        {
            ImGui.TextUnformatted("Seconds after");
            ImGui.SameLine(160 * UiHelpers.Scale);
            ImGui.SetNextItemWidth(120 * UiHelpers.Scale);
            var offset = entry.OffsetSeconds;
            if (ImGui.InputFloat("##entry-offset", ref offset, 0.5f, 1f, "%.1f", ImGuiInputTextFlags.None))
            {
                entry.OffsetSeconds = MathF.Max(0f, offset);
                MarkDirty();
            }
        }

        if (entry.Trigger == TriggerKind.CombatTime)
        {
            ImGui.TextUnformatted("Time from pull");
            ImGui.SameLine(160 * UiHelpers.Scale);
            ImGui.SetNextItemWidth(120 * UiHelpers.Scale);

            if (timeBufferFor != entry.Id)
            {
                timeBufferFor = entry.Id;
                timeBuffer = CallTemplate.FormatTime(entry.TimeSeconds);
            }

            if (UiHelpers.InputTextHint("##entry-time", "m:ss", ref timeBuffer, 12))
            {
                if (CallTemplate.TryParseTime(timeBuffer, out var seconds))
                {
                    entry.TimeSeconds = seconds;
                    entry.SortTime = seconds;
                    MarkDirty();
                }
            }
        }

        ImGui.TextUnformatted("Call this early");
        ImGui.SameLine(160 * UiHelpers.Scale);
        ImGui.SetNextItemWidth(120 * UiHelpers.Scale);
        var lead = entry.LeadSeconds;
        if (ImGui.InputFloat("##entry-lead", ref lead, 0.5f, 1f, "%.1f s", ImGuiInputTextFlags.None))
        {
            entry.LeadSeconds = MathF.Max(0f, lead);
            MarkDirty();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "For a boss cast, this is measured back from the moment the cast resolves. A 5 second " +
            "lead on a 4 second cast simply calls it the instant the cast begins.");

        ImGui.TextUnformatted("List position");
        ImGui.SameLine(160 * UiHelpers.Scale);
        ImGui.SetNextItemWidth(120 * UiHelpers.Scale);
        var sort = entry.SortTime;
        if (ImGui.InputFloat("##entry-sort", ref sort, 5f, 15f, "%.0f s", ImGuiInputTextFlags.None))
        {
            entry.SortTime = MathF.Max(0f, sort);
            MarkDirty();
        }

        if (entry.Trigger == TriggerKind.Predicted && entry.CastActionId != 0)
            DrawPredictionStatus(entry);

        ImGui.Spacing();
        ImGui.TextDisabled("Slide");
        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);
        var slidePreview = plan.FindSlide(entry.SlideId)?.Title ?? "— none —";
        if (ImGui.BeginCombo("##entry-slide", slidePreview, ImGuiComboFlags.None))
        {
            if (ImGui.Selectable("— none —", string.IsNullOrEmpty(entry.SlideId), ImGuiSelectableFlags.None, Vector2.Zero))
            {
                entry.SlideId = string.Empty;
                MarkDirty();
            }

            for (var i = 0; i < plan.Slides.Count; i++)
            {
                var s = plan.Slides[i];
                if (ImGui.Selectable($"{i + 1}. {s.Title}##es" + s.Id, s.Id == entry.SlideId, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    entry.SlideId = s.Id;
                    MarkDirty();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Who does what");
        ImGui.Separator();

        DrawAssignmentTable(plan, entry);

        ImGui.Spacing();
        ImGui.TextDisabled("What people are told");
        ImGui.Separator();

        ImGui.SetNextItemWidth(UiHelpers.WidthLeaving("(?)"));
        var callText = entry.CallText;
        if (UiHelpers.InputTextHint("##entry-call", "Team-wide call, e.g. \"Stack north — {abilities}\"", ref callText, 256))
        {
            entry.CallText = callText;
            MarkDirty();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(BuildTokenHelp());

        DrawPersonalCalls(plan, entry);

        ImGui.SetNextItemWidth(220 * UiHelpers.Scale);
        if (ImGui.BeginCombo("Audience", entry.Audience == CallAudience.Everyone ? "Everyone" : "Only people with a job here", ImGuiComboFlags.None))
        {
            if (ImGui.Selectable("Everyone", entry.Audience == CallAudience.Everyone, ImGuiSelectableFlags.None, Vector2.Zero))
            {
                entry.Audience = CallAudience.Everyone;
                MarkDirty();
            }

            if (ImGui.Selectable("Only people with a job here", entry.Audience == CallAudience.AssignedOnly, ImGuiSelectableFlags.None, Vector2.Zero))
            {
                entry.Audience = CallAudience.AssignedOnly;
                MarkDirty();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("Test call", Vector2.Zero))
            Plugin.Reminders.FireNow(plan, entry);

        ImGui.Spacing();

        var localSlot = Plugin.Roster.ResolveLocalSlot(plan);
        var preview = CallTemplate.Resolve(plan, entry, localSlot, Plugin.Config.GetActiveTeam());
        ImGui.TextDisabled("You would see:");
        ImGui.SameLine();
        ImGui.TextWrapped(string.IsNullOrWhiteSpace(preview) ? "(nothing)" : preview);
    }

    /// <summary>Shows whether the learner actually has a timing for this step yet.</summary>
    private void DrawPredictionStatus(TimelineEntry entry)
    {
        var learned = Plugin.Learner.Current?.Find(entry.CastActionId, entry.Occurrence);

        if (learned == null || learned.Samples.Count == 0)
        {
            ImGui.TextDisabled(
                "Not learned yet in this zone — this step will behave like a Boss cast trigger " +
                "until RaidPlan has watched a few pulls.");
            return;
        }

        var trusted = learned.Confidence >= Plugin.Config.MinimumPredictionConfidence;
        var colour = trusted ? new Vector4(0.5f, 0.9f, 0.5f, 1f) : new Vector4(0.9f, 0.85f, 0.45f, 1f);

        ImGui.TextColored(
            UiHelpers.Pack(colour),
            $"Learned: usually at {CallTemplate.FormatTime(learned.Median)} (±{learned.Deviation:0.0}s) " +
            $"over {learned.PullsSeen} pull(s) — {learned.ConfidenceLabel}.");

        if (!trusted)
        {
            ImGui.TextDisabled(
                $"Below the {Plugin.Config.MinimumPredictionConfidence:0.00} confidence threshold, so it " +
                "will still wait for the real cast. More pulls will fix that.");
        }
    }

    private void DrawAssignmentTable(RaidPlanDocument plan, TimelineEntry entry)
    {
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp |
                    ImGuiTableFlags.Resizable;
        if (!ImGui.BeginTable("##assignments", 3, flags, Vector2.Zero, 0f))
            return;

        ImGui.TableSetupColumn("Seat", ImGuiTableColumnFlags.WidthFixed, 150 * UiHelpers.Scale, 0);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 1.4f, 0);
        ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch, 1f, 0);
        ImGui.TableHeadersRow();

        for (var i = 0; i < plan.Roster.Count; i++)
        {
            var slot = plan.Roster[i];
            var assignments = entry.Assignments.Where(a => a.SlotIndex == i).ToList();
            var rowCount = Math.Max(1, assignments.Count + 1);

            for (var row = 0; row < rowCount; row++)
            {
                ImGui.TableNextRow();
                ImGui.PushID($"as{entry.Id}-{i}-{row}");

                ImGui.TableNextColumn();
                if (row == 0)
                    DrawSeatCell(slot);

                ImGui.TableNextColumn();
                if (row < assignments.Count)
                {
                    var assignment = assignments[row];
                    var actionId = assignment.ActionId;
                    var actionName = assignment.ActionName;
                    if (assignPicker.Draw($"##action-{entry.Id}-{i}-{row}", slot.JobId, ref actionId, ref actionName))
                    {
                        if (actionId == 0)
                        {
                            entry.Assignments.Remove(assignment);
                        }
                        else
                        {
                            assignment.ActionId = actionId;
                            assignment.ActionName = actionName;
                        }

                        MarkDirty();
                    }
                }
                else
                {
                    uint newId = 0;
                    var newName = string.Empty;
                    if (assignPicker.Draw($"##action-new-{entry.Id}-{i}", slot.JobId, ref newId, ref newName) && newId != 0)
                    {
                        entry.Assignments.Add(new Assignment
                        {
                            SlotIndex = i,
                            ActionId = newId,
                            ActionName = newName,
                        });
                        MarkDirty();
                    }
                }

                ImGui.TableNextColumn();
                if (row < assignments.Count)
                {
                    var assignment = assignments[row];
                    ImGui.SetNextItemWidth(-1);
                    var note = assignment.Note;
                    if (UiHelpers.InputTextHint("##note", "optional", ref note, 128))
                    {
                        assignment.Note = note;
                        MarkDirty();
                    }
                }

                ImGui.PopID();
            }
        }

        ImGui.EndTable();
    }

    private static void DrawSeatCell(PlayerSlot slot)
    {
        var colour = slot.Color != 0 ? slot.Color : RoleColors.Default(slot.Role);

        if (slot.JobId != 0 && UiHelpers.DrawIcon(JobIcons.For(slot.JobId), new Vector2(ImGui.GetTextLineHeight())))
            ImGui.SameLine(0, 5 * UiHelpers.Scale);

        ImGui.TextColored(colour, slot.DisplayName);

        if (!string.IsNullOrWhiteSpace(slot.Name) && ImGui.IsItemHovered())
            ImGui.SetTooltip(slot.Name);
    }

    /// <summary>Per-seat call wording. It is one value per seat, so it does not belong in the grid.</summary>
    private void DrawPersonalCalls(RaidPlanDocument plan, TimelineEntry entry)
    {
        if (!ImGui.TreeNode($"Personal wording ({entry.SlotCallText.Count} set)###personal-{entry.Id}"))
            return;

        ImGui.TextDisabled("Leave a seat blank and it hears the team-wide line.");

        for (var i = 0; i < plan.Roster.Count; i++)
        {
            var slot = plan.Roster[i];
            ImGui.PushID($"call{entry.Id}-{i}");

            var labelWidth = 120 * UiHelpers.Scale;
            DrawSeatCell(slot);
            ImGui.SameLine(labelWidth);

            entry.SlotCallText.TryGetValue(i, out var custom);
            custom ??= string.Empty;
            ImGui.SetNextItemWidth(-1);
            if (UiHelpers.InputTextHint("##slotcall", "uses the team-wide line", ref custom, 256))
            {
                if (string.IsNullOrWhiteSpace(custom))
                    entry.SlotCallText.Remove(i);
                else
                    entry.SlotCallText[i] = custom;
                MarkDirty();
            }

            ImGui.PopID();
        }

        ImGui.TreePop();
    }

    private static string BuildTokenHelp()
    {
        var lines = new List<string> { "Placeholders you can use:" };
        lines.AddRange(CallTemplate.Tokens.Select(t => $"{t.Token}  —  {t.Description}"));
        return string.Join("\n", lines);
    }

    private static string TriggerLabel(TriggerKind kind) => kind switch
    {
        TriggerKind.BossCast => "Boss cast",
        TriggerKind.AfterCast => "After a cast",
        TriggerKind.CombatTime => "Combat clock",
        TriggerKind.Predicted => "Learned timing",
        _ => "Manual only",
    };

    private static string Ordinal(int value) => value switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => value + "th",
    };
}
