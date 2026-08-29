using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace RaidPlan.Model;

/// <summary>
/// One cast at one point in a fight, across however many pulls we've seen. Times are seconds from
/// the pull. Median and MAD rather than mean and stddev, so one odd pull doesn't move it.
/// </summary>
public sealed class LearnedCast
{
    /// <summary>How many timings to keep. Older ones fall off the front.</summary>
    public const int MaxSamples = 24;

    public uint ActionId { get; set; }

    [DefaultValue("")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Which use of this action within a pull, counting from 1.</summary>
    [DefaultValue(1)]
    public int Occurrence { get; set; } = 1;

    /// <summary>Observed times from combat start, oldest first.</summary>
    public List<float> Samples { get; set; } = new();

    /// <summary>Typical cast bar length, useful for picking a sensible default lead time.</summary>
    [DefaultValue(0f)]
    public float CastBarSeconds { get; set; }

    /// <summary>Median of <see cref="Samples"/>. Recomputed whenever a sample is added.</summary>
    [DefaultValue(0f)]
    public float Median { get; set; }

    /// <summary>Median absolute deviation — how much this cast's timing wanders between pulls.</summary>
    [DefaultValue(0f)]
    public float Deviation { get; set; }

    /// <summary>Number of pulls this cast has been seen in, including ones aged out of the samples.</summary>
    [DefaultValue(0)]
    public int PullsSeen { get; set; }

    public void AddSample(float combatTime, float castBar)
    {
        Samples.Add(combatTime);
        while (Samples.Count > MaxSamples)
            Samples.RemoveAt(0);

        PullsSeen++;

        if (castBar > 0f)
        {
            // Ease towards it rather than trusting one reading.
            CastBarSeconds = CastBarSeconds <= 0f
                ? castBar
                : (CastBarSeconds * 0.7f) + (castBar * 0.3f);
        }

        Recompute();
    }

    public void Recompute()
    {
        if (Samples.Count == 0)
        {
            Median = 0f;
            Deviation = 0f;
            return;
        }

        Median = MedianOf(Samples);

        var absolute = Samples.Select(s => MathF.Abs(s - Median)).ToList();
        Deviation = MedianOf(absolute);
    }

    private static float MedianOf(List<float> values)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) * 0.5f;
    }

    /// <summary>
    /// A rough 0-1 score for how much this timing can be trusted: how many pulls back it up,
    /// tempered by how much it wanders between them.
    /// </summary>
    /// <remarks>
    /// Tolerance scales with how late the cast is: two seconds of spread three minutes in is
    /// normal pull-pace variation, the same two seconds on an opener is not.
    /// </remarks>
    public float Confidence
    {
        get
        {
            if (PullsSeen <= 0)
                return 0f;

            // Boss timelines are near-deterministic, so a few pulls agreeing is good evidence.
            var evidence = Math.Clamp(PullsSeen / 4f, 0.25f, 1f);

            var tolerance = 0.75f + (MathF.Abs(Median) * 0.02f);
            var steadiness = 1f - Math.Clamp((Deviation - tolerance) / (tolerance * 3f), 0f, 1f);

            return evidence * (0.4f + (0.6f * steadiness));
        }
    }

    public string ConfidenceLabel => Confidence switch
    {
        >= 0.75f => "solid",
        >= 0.45f => "likely",
        >= 0.2f => "rough",
        _ => "guess",
    };
}

/// <summary>Everything learned about one encounter, keyed by the territory it happens in.</summary>
public sealed class FightMemory
{
    public const int CurrentFormatVersion = 1;

    [DefaultValue(CurrentFormatVersion)]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public uint TerritoryId { get; set; }

    [DefaultValue("")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue(0)]
    public int PullCount { get; set; }

    [DefaultValue(0)]
    public int ClearCount { get; set; }

    /// <summary>Longest pull seen, which is a decent proxy for how much of the fight is known.</summary>
    [DefaultValue(0f)]
    public float LongestPullSeconds { get; set; }

    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    public List<LearnedCast> Casts { get; set; } = new();

    public LearnedCast? Find(uint actionId, int occurrence) =>
        Casts.FirstOrDefault(c => c.ActionId == actionId && c.Occurrence == occurrence);

    public LearnedCast GetOrAdd(uint actionId, int occurrence, string name)
    {
        var existing = Find(actionId, occurrence);
        if (existing != null)
        {
            if (string.IsNullOrEmpty(existing.Name) && !string.IsNullOrEmpty(name))
                existing.Name = name;
            return existing;
        }

        var created = new LearnedCast { ActionId = actionId, Occurrence = occurrence, Name = name };
        Casts.Add(created);
        return created;
    }

    /// <summary>Learned casts in the order they happen, earliest first.</summary>
    public IEnumerable<LearnedCast> InOrder() => Casts.OrderBy(c => c.Median);
}
