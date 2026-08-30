using System;
using System.Collections.Generic;
using System.Numerics;

namespace RaidPlan.Services.RaidPlanIo;

/// <summary>
/// Maps a foreign plan's pixel coordinates onto our normalised 0-1 arena.
/// </summary>
/// <remarks>
/// There is no way to know the source canvas size from the file, so the frame is fitted to the
/// plan's own contents instead: everything the plan draws, across every step, plus a margin.
/// One frame for the whole plan rather than one per slide, so a token that does not move between
/// two slides lands in the same place on both. The fit keeps the aspect ratio, so a wide plan is
/// letterboxed rather than stretched into the square.
/// </remarks>
public readonly struct PlanFrame
{
    public PlanFrame(float minX, float minY, float side)
    {
        MinX = minX;
        MinY = minY;
        Side = side <= 0f ? 1f : side;
    }

    public float MinX { get; }

    public float MinY { get; }

    /// <summary>Side of the square the source is mapped from. Square, so shapes do not skew.</summary>
    public float Side { get; }

    public static PlanFrame Fit(IReadOnlyList<Vector2> points, float padding)
    {
        if (points.Count == 0)
            return new PlanFrame(0f, 0f, 1f);

        var min = points[0];
        var max = points[0];

        foreach (var p in points)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        var size = max - min;
        var side = MathF.Max(MathF.Max(size.X, size.Y), 1f) * (1f + (padding * 2f));

        // Centre the content in the square, so the letterboxing is even on both sides.
        var centre = (min + max) * 0.5f;

        return new PlanFrame(centre.X - (side * 0.5f), centre.Y - (side * 0.5f), side);
    }

    public Vector2 Normalise(float x, float y) => new((x - MinX) / Side, (y - MinY) / Side);

    /// <summary>A length in source pixels as a fraction of the arena.</summary>
    public float Length(float pixels) => pixels / Side;
}
