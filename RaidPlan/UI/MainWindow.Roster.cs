using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RaidPlan.Model;
using RaidPlan.Services;

namespace RaidPlan.UI;

public sealed partial class MainWindow
{
    private void DrawRosterTab(RaidPlanDocument plan)
    {
        ImGui.TextWrapped(
            "Seats, not names, are what the plan is written against — swap a body in and every " +
            "assignment follows the seat.");

        ImGui.Spacing();

        if (ImGui.Button("Fill from my party", Vector2.Zero))
        {
            var placed = Plugin.Roster.FillFromParty(plan);
            MarkDirty();
            Plugin.ChatGui.Print(
                placed == 0 ? "No party members found to place." : $"Placed {placed} player(s) into the roster.",
                "RaidPlan",
                null);
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear names", Vector2.Zero))
        {
            foreach (var slot in plan.Roster)
            {
                slot.Name = string.Empty;
                slot.JobId = 0;
            }

            MarkDirty();
        }

        ImGui.SameLine();
        if (plan.Roster.Count < 24 && ImGui.Button("Add seat", Vector2.Zero))
        {
            plan.Roster.Add(new PlayerSlot { Placeholder = "P" + (plan.Roster.Count + 1) });
            MarkDirty();
        }

        if (plan.Roster.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.Button("Remove last seat", Vector2.Zero))
            {
                var index = plan.Roster.Count - 1;
                plan.Roster.RemoveAt(index);

                // Clean up anything pointing at the seat that just went away.
                foreach (var entry in plan.Timeline)
                {
                    entry.Assignments.RemoveAll(a => a.SlotIndex >= plan.Roster.Count);
                    entry.SlotCallText.Remove(index);
                }

                foreach (var slide in plan.Slides)
                {
                    foreach (var item in slide.Items.Where(i => i.SlotIndex >= plan.Roster.Count))
                        item.SlotIndex = -1;
                }

                MarkDirty();
            }
        }

        ImGui.Separator();

        var localSlot = Plugin.Roster.ResolveLocalSlot(plan);

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("##roster", 5, flags, Vector2.Zero, 0f))
            return;

        ImGui.TableSetupColumn("Seat", ImGuiTableColumnFlags.WidthFixed, 120 * UiHelpers.Scale, 0);
        ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch, 0f, 0);
        ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 160 * UiHelpers.Scale, 0);
        ImGui.TableSetupColumn("Role", ImGuiTableColumnFlags.WidthFixed, 140 * UiHelpers.Scale, 0);
        ImGui.TableSetupColumn("Colour", ImGuiTableColumnFlags.WidthFixed, 90 * UiHelpers.Scale, 0);
        ImGui.TableHeadersRow();

        for (var i = 0; i < plan.Roster.Count; i++)
        {
            var slot = plan.Roster[i];
            ImGui.TableNextRow();
            ImGui.PushID("seat" + i);

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(i == localSlot ? UiHelpers.WidthLeaving("you") : -1);
            var placeholder = slot.Placeholder;
            if (UiHelpers.InputTextHint("##placeholder", "MT", ref placeholder, 16))
            {
                slot.Placeholder = placeholder;
                MarkDirty();
            }

            if (i == localSlot)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("you");
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var name = slot.Name;
            if (UiHelpers.InputTextHint("##name", "character name", ref name, 64))
            {
                slot.Name = name;
                MarkDirty();
            }

            ImGui.TableNextColumn();
            var iconSize = new Vector2(ImGui.GetFrameHeight());
            if (slot.JobId != 0 && UiHelpers.DrawIcon(JobIcons.For(slot.JobId), iconSize))
                ImGui.SameLine(0, 4 * UiHelpers.Scale);

            ImGui.SetNextItemWidth(-1);
            var jobPreview = slot.JobId == 0 ? "— any —" : Plugin.Actions.Job(slot.JobId)?.Name ?? "?";
            if (ImGui.BeginCombo("##job", jobPreview, ImGuiComboFlags.HeightLarge))
            {
                if (ImGui.Selectable("— any —", slot.JobId == 0, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    slot.JobId = 0;
                    MarkDirty();
                }

                var rowIcon = new Vector2(ImGui.GetTextLineHeight());
                RaidRole lastRole = RaidRole.Unknown;

                foreach (var job in Plugin.Actions.Jobs.Where(j => j.IsCombatJob))
                {
                    // Group the list by role so picking a caster means scanning four entries.
                    if (job.Role != lastRole)
                    {
                        if (lastRole != RaidRole.Unknown)
                            ImGui.Separator();
                        ImGui.TextDisabled(RoleColors.Label(job.Role));
                        lastRole = job.Role;
                    }

                    var cursor = ImGui.GetCursorPosY();
                    var picked = ImGui.Selectable($"##job{job.RowId}", job.RowId == slot.JobId,
                        ImGuiSelectableFlags.None, new Vector2(0, rowIcon.Y));

                    ImGui.SetCursorPosY(cursor);
                    if (UiHelpers.DrawIcon(JobIcons.For(job.RowId), rowIcon))
                        ImGui.SameLine(0, 6 * UiHelpers.Scale);
                    ImGui.TextUnformatted($"{job.Abbreviation}  {job.Name}");

                    if (!picked)
                        continue;

                    slot.JobId = job.RowId;
                    if (job.Role != RaidRole.Unknown)
                    {
                        slot.Role = job.Role;
                        if (slot.Color == 0)
                            slot.Color = RoleColors.Default(job.Role);
                    }

                    MarkDirty();
                }

                ImGui.EndCombo();
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##role", RoleColors.Label(slot.Role), ImGuiComboFlags.None))
            {
                foreach (var role in Enum.GetValues<RaidRole>())
                {
                    if (ImGui.Selectable(RoleColors.Label(role), role == slot.Role, ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        slot.Role = role;
                        MarkDirty();
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.TableNextColumn();
            var colour = slot.Color != 0 ? slot.Color : RoleColors.Default(slot.Role);
            if (UiHelpers.ColorButton("##colour", ref colour))
            {
                slot.Color = colour;
                MarkDirty();
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        ImGui.Spacing();
        ImGui.Separator();

        var team = Plugin.Config.GetActiveTeam();
        ImGui.TextDisabled("Which seat am I?");
        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Calls are addressed to a seat. RaidPlan matches your character name against the roster; " +
            "pin a seat here if you would rather say it outright, or if you play the same plan on alts.");

        ImGui.SetNextItemWidth(260 * UiHelpers.Scale);
        var pinnedPreview = team.PinnedSlotIndex >= 0 && team.PinnedSlotIndex < plan.Roster.Count
            ? SeatLabel(plan, team.PinnedSlotIndex)
            : "Work it out from my character name";

        if (ImGui.BeginCombo("##pinned-slot", pinnedPreview, ImGuiComboFlags.None))
        {
            if (ImGui.Selectable("Work it out from my character name", team.PinnedSlotIndex < 0, ImGuiSelectableFlags.None, Vector2.Zero))
            {
                team.PinnedSlotIndex = -1;
                Plugin.SaveConfig();
            }

            for (var i = 0; i < plan.Roster.Count; i++)
            {
                if (ImGui.Selectable(SeatLabel(plan, i) + "##pin" + i, team.PinnedSlotIndex == i, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    team.PinnedSlotIndex = i;
                    Plugin.SaveConfig();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.TextDisabled(localSlot >= 0
            ? $"→ currently {SeatLabel(plan, localSlot)}"
            : "→ no seat matched; you will only see team-wide calls");
    }
}
