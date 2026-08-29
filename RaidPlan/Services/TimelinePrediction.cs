using System;

namespace RaidPlan.Services;

/// <summary>Prediction arithmetic, split out so the sign conventions can be tested directly.</summary>
public static class TimelinePrediction
{
    /// <summary>Past this the pull is doing something we don't understand; don't shift by it.</summary>
    public const float MaxDriftSeconds = 120f;

    /// <summary>How well we have to know a cast before it may re-anchor the fight.</summary>
    public const float MinimumAnchorConfidence = 0.3f;

    /// <summary>How far behind (+) or ahead (-) of the usual timings this pull is running.</summary>
    public static float MeasureDrift(float actualCombatTime, float learnedMedian)
    {
        var drift = actualCombatTime - learnedMedian;
        return Math.Clamp(drift, -MaxDriftSeconds, MaxDriftSeconds);
    }

    /// <summary>When a cast is expected in the current pull, given its usual time and the drift.</summary>
    public static float Expected(float learnedMedian, float drift) => learnedMedian + drift;

    /// <summary>Lead is measured back from the mechanic, so an over-long lead just means now.</summary>
    public static bool IsDue(float combatElapsed, float expectedTime, float leadSeconds) =>
        combatElapsed >= expectedTime - leadSeconds;
}
