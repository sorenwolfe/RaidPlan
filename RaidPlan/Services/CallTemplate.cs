using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RaidPlan.Model;

namespace RaidPlan.Services;

/// <summary>
/// Expands the placeholders a team can put in their shotcall text.
/// </summary>
public static class CallTemplate
{
    /// <summary>The tokens a raid lead can use, with a one-line explanation for the UI.</summary>
    public static readonly (string Token, string Description)[] Tokens =
    {
        ("{label}", "The name of the timeline step, e.g. \"Akh Morn 1\""),
        ("{cast}", "The boss cast this step is anchored to"),
        ("{player}", "The player in the seat this call is for"),
        ("{job}", "That player's job abbreviation"),
        ("{seat}", "The seat label, e.g. \"H2\""),
        ("{ability}", "That seat's first assigned action on this step"),
        ("{abilities}", "All of that seat's assigned actions, comma separated"),
        ("{note}", "The note attached to that seat's assignment"),
        ("{time}", "The step's timeline position as m:ss"),
        ("{lead}", "How many seconds early the call fires"),
        ("{slide}", "Title of the slide linked to this step"),
        ("{team}", "The active team profile's name"),
    };

    public static string Render(
        string template,
        RaidPlanDocument plan,
        TimelineEntry entry,
        int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var slot = slotIndex >= 0 && slotIndex < plan.Roster.Count ? plan.Roster[slotIndex] : null;
        var assignments = entry.ForSlot(slotIndex).ToList();

        var abilities = assignments
            .Select(a => Plugin.Actions.NameOf(a.ActionId, a.ActionName))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var note = assignments.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Note))?.Note ?? string.Empty;
        var slide = plan.FindSlide(entry.SlideId);

        var sb = new StringBuilder(template);
        Replace(sb, "{label}", entry.Label);
        Replace(sb, "{cast}", string.IsNullOrWhiteSpace(entry.CastName)
            ? Plugin.Actions.NameOf(entry.CastActionId, entry.CastName)
            : entry.CastName);
        Replace(sb, "{player}", slot?.Name ?? string.Empty);
        Replace(sb, "{job}", slot != null && slot.JobId != 0 ? Plugin.Actions.JobAbbreviation(slot.JobId) : string.Empty);
        Replace(sb, "{seat}", slot?.DisplayName ?? string.Empty);
        Replace(sb, "{ability}", abilities.FirstOrDefault() ?? string.Empty);
        Replace(sb, "{abilities}", string.Join(" + ", abilities));
        Replace(sb, "{note}", note);
        Replace(sb, "{time}", FormatTime(entry.SortTime));
        Replace(sb, "{lead}", entry.LeadSeconds.ToString("0.#"));
        Replace(sb, "{slide}", slide?.Title ?? string.Empty);
        Replace(sb, "{team}", Plugin.Config.GetActiveTeam().Name);

        return Tidy(sb.ToString());
    }

    /// <summary>
    /// Picks the line a seat should hear: its own override, then the entry default, then the
    /// team's fallback template.
    /// </summary>
    public static string Resolve(RaidPlanDocument plan, TimelineEntry entry, int slotIndex, TeamProfile team)
    {
        if (slotIndex >= 0 && entry.SlotCallText.TryGetValue(slotIndex, out var custom) && !string.IsNullOrWhiteSpace(custom))
            return Render(custom, plan, entry, slotIndex);

        if (!string.IsNullOrWhiteSpace(entry.CallText))
            return Render(entry.CallText, plan, entry, slotIndex);

        return Render(team.DefaultTemplate, plan, entry, slotIndex);
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0)
            seconds = 0;
        var span = TimeSpan.FromSeconds(seconds);
        return $"{(int)span.TotalMinutes}:{span.Seconds:00}";
    }

    public static bool TryParseTime(string text, out float seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        var parts = text.Split(':');
        try
        {
            if (parts.Length == 1)
                return float.TryParse(parts[0], out seconds);

            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var minutes) &&
                float.TryParse(parts[1], out var secs))
            {
                seconds = minutes * 60 + secs;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static void Replace(StringBuilder sb, string token, string value)
    {
        sb.Replace(token, value ?? string.Empty);
    }

    /// <summary>Collapses the gaps left behind when a token expands to nothing.</summary>
    private static string Tidy(string value)
    {
        var sb = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var c in value)
        {
            if (c == ' ')
            {
                if (lastWasSpace)
                    continue;
                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            sb.Append(c);
        }

        return sb.ToString().Trim().Trim('-', ':', ',').Trim();
    }
}
