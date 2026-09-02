using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Shikari.Services.RaidPlanIo;

/// <summary>One line of a fight timeline written out in a plan's notes.</summary>
public sealed record NoteTimelineEntry(float Seconds, string Label, int FirstSlide, int LastSlide)
{
    public bool HasSlide => FirstSlide > 0;
}

/// <summary>
/// Plans usually carry their fight timeline as plain text in the notes, in the shape
/// <c>04:12 - Double Alley-oop (slides 15-18)</c>. That is a real timeline someone typed out by
/// hand, and it costs nothing to read it rather than leave it as a wall of text.
/// </summary>
public static class NoteTimeline
{
    // mm:ss, then a dash of some kind, then the name, then an optional slide reference.
    private static readonly Regex Line = new(
        @"^\s*(?<m>\d{1,2}):(?<s>\d{2})\s*[-–—:]\s*(?<label>.+?)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex SlideRef = new(
        @"\(\s*slides?\s+(?<from>\d{1,3})\s*(?:[-–—to]+\s*(?<to>\d{1,3}))?\s*\)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryParseLine(string? line, out NoteTimelineEntry entry)
    {
        entry = new NoteTimelineEntry(0f, string.Empty, 0, 0);

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var match = Line.Match(line);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["m"].Value, out var minutes) ||
            !int.TryParse(match.Groups["s"].Value, out var seconds) ||
            seconds > 59)
        {
            return false;
        }

        var label = match.Groups["label"].Value.Trim();
        var first = 0;
        var last = 0;

        var slides = SlideRef.Match(label);
        if (slides.Success)
        {
            int.TryParse(slides.Groups["from"].Value, out first);
            last = slides.Groups["to"].Success && int.TryParse(slides.Groups["to"].Value, out var to)
                ? to
                : first;

            if (last < first)
                (first, last) = (last, first);

            label = label[..slides.Index].Trim();
        }

        if (label.Length == 0)
            return false;

        return Assign(out entry, new NoteTimelineEntry((minutes * 60) + seconds, label, first, last));
    }

    private static bool Assign(out NoteTimelineEntry target, NoteTimelineEntry value)
    {
        target = value;
        return true;
    }

    public static List<NoteTimelineEntry> Parse(string? notes)
    {
        var found = new List<NoteTimelineEntry>();
        if (string.IsNullOrWhiteSpace(notes))
            return found;

        foreach (var line in notes.Replace("\r\n", "\n").Split('\n'))
        {
            if (TryParseLine(line, out var entry))
                found.Add(entry);
        }

        return found;
    }
}
