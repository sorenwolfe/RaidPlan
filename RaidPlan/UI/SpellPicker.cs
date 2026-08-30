using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RaidPlan.Services;

namespace RaidPlan.UI;

/// <summary>
/// A searchable dropdown over the game's action list. One instance can serve many rows;
/// the search box resets whenever the popup is reopened on a different target.
/// </summary>
public sealed class SpellPicker
{
    private string search = string.Empty;
    private string openFor = string.Empty;
    private List<ActionEntry> results = new();
    // Deliberately not "": an empty search is a legitimate state, so the "nothing has been
    // searched yet" sentinel has to be a value the box can never hold.
    private string? lastQuery;
    private uint lastJob = uint.MaxValue;
    private bool lastPlayerOnly = true;
    private bool lastCooldownsOnly = true;

    /// <summary>
    /// Draws the picker. Returns true when the selection changed.
    /// </summary>
    /// <param name="id">Unique ImGui id, including the leading ## if no label is wanted.</param>
    /// <param name="jobId">Restrict player actions to this job, or 0 for no restriction.</param>
    /// <param name="actionId">Currently selected action row id.</param>
    /// <param name="actionName">Cached name, updated alongside the id.</param>
    /// <param name="playerActionsOnly">False to search every named action, including boss casts.</param>
    /// <param name="width">Width of the combo, or -1 to fill.</param>
    public bool Draw(string id, uint jobId, ref uint actionId, ref string actionName,
        bool playerActionsOnly = true, float width = -1f)
    {
        var changed = false;

        var preview = actionId == 0
            ? "— none —"
            : Plugin.Actions.NameOf(actionId, actionName);

        if (width > 0)
            ImGui.SetNextItemWidth(width);
        else
            ImGui.SetNextItemWidth(-1f);

        // The combo lives in a narrow table column, and the popup would inherit that width.
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(360 * UiHelpers.Scale, 0),
            new Vector2(float.MaxValue, float.MaxValue));

        if (!ImGui.BeginCombo(id, preview, ImGuiComboFlags.HeightLarge))
            return false;

        if (openFor != id)
        {
            openFor = id;
            search = string.Empty;
            lastQuery = null;
            ImGui.SetKeyboardFocusHere();
        }

        if (!Plugin.Actions.Ready)
        {
            ImGui.TextDisabled("Still reading the game's action list…");
            ImGui.EndCombo();
            return false;
        }

        var cooldownsOnly = Plugin.Config.CooldownsOnly;

        if (playerActionsOnly)
        {
            ImGui.SetNextItemWidth(UiHelpers.WidthLeaving("Cooldowns only"));
        }
        else
        {
            ImGui.SetNextItemWidth(-1f);
        }

        UiHelpers.InputTextHint("##spell-search", "Type to search…", ref search, 128);

        if (playerActionsOnly)
        {
            ImGui.SameLine();
            if (ImGui.Checkbox("Cooldowns only", ref cooldownsOnly))
            {
                Plugin.Config.CooldownsOnly = cooldownsOnly;
                Plugin.SaveConfig();
            }

            if (ImGui.IsItemHovered())
            {
                UiHelpers.Tooltip(
                    "Anything on a 30 second or longer recast, plus the role actions. That's " +
                    "mitigation, utility and burst windows, without the rotation.");
            }
        }

        if (search != lastQuery || jobId != lastJob || playerActionsOnly != lastPlayerOnly ||
            cooldownsOnly != lastCooldownsOnly)
        {
            lastQuery = search;
            lastJob = jobId;
            lastPlayerOnly = playerActionsOnly;
            lastCooldownsOnly = cooldownsOnly;
            results = playerActionsOnly
                ? Plugin.Actions.SearchPlayerActions(search, jobId, cooldownsOnly)
                : Plugin.Actions.SearchAllActions(search);
        }

        ImGui.Separator();

        var rowHeight = ImGui.GetTextLineHeightWithSpacing() + (6f * UiHelpers.Scale);
        var listHeight = Math.Min(14, Math.Max(4, results.Count + 1)) * rowHeight;

        if (ImGui.BeginChild("##spell-results", new Vector2(0, listHeight), false, ImGuiWindowFlags.HorizontalScrollbar))
        {
            if (actionId != 0)
            {
                if (ImGui.Selectable("— clear —", false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    actionId = 0;
                    actionName = string.Empty;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }
            }

            if (results.Count == 0)
            {
                ImGui.TextDisabled(search.Length == 0
                    ? "Start typing to search."
                    : "Nothing matched that.");
            }

            var iconSize = new Vector2(rowHeight - (4f * UiHelpers.Scale));

            foreach (var entry in results)
            {
                ImGui.PushID("row" + entry.RowId);

                var cursor = ImGui.GetCursorPosY();
                var selected = entry.RowId == actionId;

                if (ImGui.Selectable("##row", selected, ImGuiSelectableFlags.None, new Vector2(0, iconSize.Y)))
                {
                    actionId = entry.RowId;
                    actionName = entry.Name;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SetCursorPosY(cursor);
                if (UiHelpers.DrawIcon(entry.IconId, iconSize))
                    ImGui.SameLine();

                ImGui.TextUnformatted(entry.Name);

                if (!string.IsNullOrEmpty(entry.JobAbbreviation) || entry.IsRoleAction)
                {
                    ImGui.SameLine();
                    var tag = entry.IsRoleAction ? "Role" : entry.JobAbbreviation;
                    ImGui.TextDisabled($"({tag})");
                }

                if (entry.RecastSeconds >= 1f)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"{entry.RecastSeconds:0}s CD");
                }

                ImGui.PopID();
            }
        }

        ImGui.EndChild();
        ImGui.EndCombo();

        return changed;
    }
}
