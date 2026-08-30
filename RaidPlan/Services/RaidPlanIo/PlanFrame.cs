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

        return Around((min + max) * 0.5f, points, padding);
    }

    /// <summary>
    /// Fits around a given centre rather than the middle of the bounding box.
    /// </summary>
    /// <remarks>
    /// Given the waymark ring's centre this keeps the arena in the middle of the square. A plan
    /// drawn lopsided — everything happening in the north half — otherwise gets its whole arena
    /// shoved off-centre to make room for empty space.
    /// </remarks>
    public static PlanFrame Around(Vector2 centre, IReadOnlyList<Vector2> points, float padding)
    {
        if (points.Count == 0)
            return new PlanFrame(centre.X - 0.5f, centre.Y - 0.5f, 1f);

        var reach = 0f;
        foreach (var p in points)
        {
            reach = MathF.Max(reach, MathF.Abs(p.X - centre.X));
            reach = MathF.Max(reach, MathF.Abs(p.Y - centre.Y));
        }

        var side = MathF.Max(reach * 2f, 1f) * (1f + (padding * 2f));

        return new PlanFrame(centre.X - (side * 0.5f), centre.Y - (side * 0.5f), side);
    }

    /// <summary>
    /// Sizes the frame from the source arena itself rather than from whatever is drawn on it.
    /// </summary>
    /// <remarks>
    /// This is the difference between a plan that imports at the right size and one that shrinks
    /// because somebody drew a wide mechanic on step 30. Fitting the contents makes the scale
    /// depend on the busiest slide in the plan; fitting the arena makes two plans of the same
    /// fight come in at the same size, which is what anyone comparing them expects.
    /// </remarks>
    /// <param name="centre">Middle of the source arena.</param>
    /// <param name="arenaRadius">Its radius, in source pixels.</param>
    /// <param name="edge">Where our own arena outline sits, as a fraction of the square.</param>
    public static PlanFrame FromArena(Vector2 centre, float arenaRadius, float edge)
    {
        var side = MathF.Max(arenaRadius, 1f) / Math.Clamp(edge, 0.05f, 0.5f);

        return new PlanFrame(centre.X - (side * 0.5f), centre.Y - (side * 0.5f), side);
    }

    /// <summary>How far the furthest of these points sits from the frame's middle, 0.5 being the edge.</summary>
    public float Reach(IReadOnlyList<Vector2> points)
    {
        var centre = new Vector2(MinX + (Side * 0.5f), MinY + (Side * 0.5f));
        var reach = 0f;

        foreach (var p in points)
        {
            reach = MathF.Max(reach, MathF.Abs(p.X - centre.X));
            reach = MathF.Max(reach, MathF.Abs(p.Y - centre.Y));
        }

        return reach / Side;
    }

    /// <summary>The same frame widened by a factor, keeping its centre.</summary>
    public PlanFrame Widened(float factor)
    {
        var side = Side * MathF.Max(1f, factor);
        var centre = new Vector2(MinX + (Side * 0.5f), MinY + (Side * 0.5f));

        return new PlanFrame(centre.X - (side * 0.5f), centre.Y - (side * 0.5f), side);
    }

    public Vector2 Normalise(float x, float y) => new((x - MinX) / Side, (y - MinY) / Side);

    /// <summary>A length in source pixels as a fraction of the arena.</summary>
    public float Length(float pixels) => pixels / Side;
}
