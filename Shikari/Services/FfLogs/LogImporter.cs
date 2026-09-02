using System;
using System.Collections.Generic;
using System.Linq;
using Shikari.Model;

namespace Shikari.Services.FfLogs;

public sealed class ImportOptions
{
    public bool ImportTimeline { get; set; } = true;
    public bool ImportAssignments { get; set; } = true;
    public bool CreateSlides { get; set; }

    /// <summary>Skip instant casts, which are mostly auto-attacks and filler.</summary>
    public bool OnlyCastsWithBar { get; set; } = true;

    /// <summary>An ability the boss uses more often than this is spam, not a mechanic.</summary>
    public int MaxOccurrences { get; set; } = 12;

    public float LeadSeconds { get; set; } = 5f;

    /// <summary>A cooldown pressed this long before a cast is counted as being for it.</summary>
    public float WindowBefore { get; set; } = 20f;

    /// <summary>And this long after, for reactive presses.</summary>
    public float WindowAfter { get; set; } = 6f;
}

public sealed class ImportResult
{
    public int StepsAdded { get; set; }
    public int StepsSkipped { get; set; }
    public int SlidesAdded { get; set; }
    public int AssignmentsAdded { get; set; }
    public int CooldownsUnattributed { get; set; }
    public List<SeatMatch> Matches { get; } = new();
    public List<string> Unmatched { get; } = new();

    public string Summary()
    {
        var parts = new List<string>();
        if (StepsAdded > 0) parts.Add($"{StepsAdded} step(s)");
        if (SlidesAdded > 0) parts.Add($"{SlidesAdded} slide(s)");
        if (AssignmentsAdded > 0) parts.Add($"{AssignmentsAdded} assignment(s)");
        if (parts.Count == 0) return "Nothing new to import.";

        var text = "Imported " + string.Join(", ", parts) + ".";
        if (StepsSkipped > 0) text += $" {StepsSkipped} already on the timeline.";
        return text;
    }
}

/// <summary>A seat in the plan paired with the player in the log who was on that job.</summary>
public readonly record struct SeatMatch(int SeatIndex, int ActorId, string PlayerName, string JobName);

/// <summary>A seat and the job it is set to, as plain text so the matching can be tested.</summary>
public readonly record struct SeatJob(int SeatIndex, string JobName, string Abbreviation);

/// <summary>
/// Turns a fight out of a log into timeline steps, and the cooldowns people actually pressed into
/// assignments on those steps.
/// </summary>
public static class LogImporter
{
    /// <summary>
    /// Pairs plan seats to log players by job. Where a job appears more than once, seats and
    /// players are paired in the order they appear, which is the best guess available.
    /// </summary>
    public static List<SeatMatch> MatchSeats(IReadOnlyList<SeatJob> seats, IReadOnlyList<LogActor> players)
    {
        var matches = new List<SeatMatch>();
        var taken = new HashSet<int>();

        foreach (var seat in seats)
        {
            if (string.IsNullOrWhiteSpace(seat.JobName))
                continue;

            foreach (var player in players)
            {
                if (!player.IsPlayer || taken.Contains(player.Id))
                    continue;
                if (!SameJob(player.Job, seat.JobName, seat.Abbreviation))
                    continue;

                matches.Add(new SeatMatch(seat.SeatIndex, player.Id, player.Name, player.Job));
                taken.Add(player.Id);
                break;
            }
        }

        return matches;
    }

    /// <summary>
    /// Logs spell job names their own way, so compare with the punctuation and spacing removed.
    /// </summary>
    public static bool SameJob(string logJob, string jobName, string abbreviation)
    {
        var a = Normalise(logJob);
        if (a.Length == 0)
            return false;

        return a == Normalise(jobName) || a == Normalise(abbreviation);
    }

    private static string Normalise(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
                buffer[length++] = char.ToLowerInvariant(c);
        }

        return new string(buffer[..length]);
    }

    /// <summary>
    /// Which step a cooldown belongs to. People press mitigation ahead of the mechanic, so a cast
    /// shortly before a step counts for it; a cast shortly after covers reactive presses.
    /// </summary>
    public static int AttributeToStep(float castTime, IReadOnlyList<float> stepTimes, float windowBefore, float windowAfter)
    {
        var best = -1;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < stepTimes.Count; i++)
        {
            var delta = stepTimes[i] - castTime;

            // Positive delta: the step is still ahead of the cast.
            var within = delta >= 0 ? delta <= windowBefore : -delta <= windowAfter;
            if (!within)
                continue;

            // Prefer the upcoming step over one already past, then the closest.
            var distance = delta >= 0 ? delta : (-delta) + windowBefore;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = i;
        }

        return best;
    }

    /// <summary>Turns the enemy casts into the steps of a timeline, in order.</summary>
    public static List<TimelineEntry> BuildTimeline(LogFightData data, ImportOptions options)
    {
        var counts = new Dictionary<uint, int>();
        var totals = new Dictionary<uint, int>();

        foreach (var cast in data.EnemyCasts)
            totals[cast.AbilityId] = totals.GetValueOrDefault(cast.AbilityId) + 1;

        var steps = new List<TimelineEntry>();

        foreach (var cast in data.EnemyCasts.OrderBy(c => c.TimeSeconds))
        {
            if (options.OnlyCastsWithBar && cast.CastSeconds <= 0f)
                continue;
            if (totals[cast.AbilityId] > options.MaxOccurrences)
                continue;

            var occurrence = counts.GetValueOrDefault(cast.AbilityId) + 1;
            counts[cast.AbilityId] = occurrence;

            var name = !string.IsNullOrWhiteSpace(cast.AbilityName)
                ? cast.AbilityName
                : data.AbilityNames.GetValueOrDefault(cast.AbilityId, "Action #" + cast.AbilityId);

            steps.Add(new TimelineEntry
            {
                Label = occurrence <= 1 ? name : $"{name} {occurrence}",
                Trigger = TriggerKind.BossCast,
                CastActionId = cast.AbilityId,
                CastName = name,
                Occurrence = occurrence,
                SortTime = cast.TimeSeconds,
                LeadSeconds = Math.Clamp(MathF.Max(cast.CastSeconds, options.LeadSeconds), 2f, 10f),
            });
        }

        return steps;
    }

    /// <summary>
    /// Writes the fight into the plan. <paramref name="isCooldown"/> decides which player actions
    /// are worth carrying over, and <paramref name="seats"/> is the plan's roster as job text.
    /// </summary>
    public static ImportResult Apply(
        PlanDocument plan,
        LogFightData data,
        ImportOptions options,
        IReadOnlyList<SeatJob> seats,
        Func<uint, bool> isCooldown)
    {
        var result = new ImportResult();

        var imported = BuildTimeline(data, options);
        var stepsForAttribution = new List<TimelineEntry>();

        if (options.ImportTimeline)
        {
            foreach (var step in imported)
            {
                var existing = plan.Timeline.FirstOrDefault(e =>
                    e.CastActionId == step.CastActionId && e.Occurrence == step.Occurrence);

                if (existing != null)
                {
                    result.StepsSkipped++;
                    stepsForAttribution.Add(existing);
                    continue;
                }

                if (options.CreateSlides)
                {
                    var slide = new Slide { Title = step.Label };
                    plan.Slides.Add(slide);
                    step.SlideId = slide.Id;
                    result.SlidesAdded++;
                }

                plan.Timeline.Add(step);
                stepsForAttribution.Add(step);
                result.StepsAdded++;
            }
        }
        else
        {
            // Attribute onto whatever is already there.
            foreach (var step in imported)
            {
                var existing = plan.Timeline.FirstOrDefault(e =>
                    e.CastActionId == step.CastActionId && e.Occurrence == step.Occurrence);
                if (existing != null)
                    stepsForAttribution.Add(existing);
            }
        }

        if (!options.ImportAssignments || stepsForAttribution.Count == 0)
            return result;

        var matches = MatchSeats(seats, data.Actors);
        result.Matches.AddRange(matches);

        foreach (var player in data.Actors.Where(a => a.IsPlayer))
        {
            if (matches.All(m => m.ActorId != player.Id))
                result.Unmatched.Add($"{player.Name} ({player.Job})");
        }

        var times = stepsForAttribution.Select(s => s.SortTime).ToList();

        foreach (var match in matches)
        {
            var casts = data.PlayerCasts
                .Where(c => c.SourceId == match.ActorId && isCooldown(c.AbilityId))
                .OrderBy(c => c.TimeSeconds);

            foreach (var cast in casts)
            {
                var index = AttributeToStep(cast.TimeSeconds, times, options.WindowBefore, options.WindowAfter);
                if (index < 0)
                {
                    result.CooldownsUnattributed++;
                    continue;
                }

                var step = stepsForAttribution[index];

                var already = step.Assignments.Any(a =>
                    a.SlotIndex == match.SeatIndex && a.ActionId == cast.AbilityId);
                if (already)
                    continue;

                step.Assignments.Add(new Assignment
                {
                    SlotIndex = match.SeatIndex,
                    ActionId = cast.AbilityId,
                    ActionName = !string.IsNullOrWhiteSpace(cast.AbilityName)
                        ? cast.AbilityName
                        : data.AbilityNames.GetValueOrDefault(cast.AbilityId, string.Empty),
                });

                result.AssignmentsAdded++;
            }
        }

        return result;
    }
}
