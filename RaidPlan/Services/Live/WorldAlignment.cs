using System;
using System.Collections.Generic;
using System.Numerics;

namespace RaidPlan.Services.Live;

/// <summary>One waymark seen in both places: where the game has it, where the plan drew it.</summary>
public readonly record struct AlignmentPair(Vector2 World, Vector2 Plan);

/// <summary>
/// Maps the arena's world coordinates onto the plan's 0-1 square.
/// </summary>
/// <remarks>
/// The waymarks are the one thing that exists in both worlds: the game knows where they are in
/// yalms, the plan knows where it drew them on the board. Line those two sets up and every other
/// world position — where each player is actually standing — can be put on the board too.
///
/// The fit is a similarity: one rotation, one uniform scale, one shift. Not a general affine,
/// deliberately. An affine fit would happily squash and skew the arena to swallow the error from
/// a plan whose waymarks are drawn roughly, and the result looks plausible while putting people
/// in the wrong place. A similarity can only be wrong in ways that show up in the residual, which
/// is why the residual is returned and acted on rather than thrown away.
/// </remarks>
public readonly struct WorldAlignment
{
    /// <summary>
    /// Worst mean error we will still draw, as a fraction of the board.
    /// </summary>
    /// <remarks>
    /// Two percent of the arena. Past that the plan's waymarks disagree with the real ones badly
    /// enough that the dots would be misleading, and a wrong dot is worse than no dot: someone
    /// moves to it.
    /// </remarks>
    public const float MaxResidual = 0.02f;

    /// <summary>Fewer than this and a bad fit cannot be told from a good one.</summary>
    public const int MinimumPairs = 3;

    private WorldAlignment(float scale, float sin, float cos, Vector2 offset, float residual)
    {
        Scale = scale;
        Sin = sin;
        Cos = cos;
        Offset = offset;
        Residual = residual;
        IsValid = true;
    }

    public bool IsValid { get; }

    /// <summary>Board units per yalm.</summary>
    public float Scale { get; }

    private float Sin { get; }

    private float Cos { get; }

    public Vector2 Offset { get; }

    /// <summary>Mean distance between where the plan put a waymark and where this fit puts it.</summary>
    public float Residual { get; }

    /// <summary>True when the fit is close enough that positions drawn from it can be trusted.</summary>
    public bool IsTrustworthy => IsValid && Residual <= MaxResidual;

    /// <summary>How far the plan is turned relative to the arena, in degrees. Handy for reporting.</summary>
    public float RotationDegrees => MathF.Atan2(Sin, Cos) * (180f / MathF.PI);

    /// <summary>A world position on the board. Feed it the horizontal plane: X and Z, not Y.</summary>
    public Vector2 ToPlan(Vector2 world)
    {
        if (!IsValid)
            return new Vector2(0.5f, 0.5f);

        return new Vector2(
            (Scale * ((world.X * Cos) - (world.Y * Sin))) + Offset.X,
            (Scale * ((world.X * Sin) + (world.Y * Cos))) + Offset.Y);
    }

    /// <summary>
    /// A board position back out into the world, so the plan can be drawn on the arena floor.
    /// </summary>
    /// <remarks>
    /// The exact inverse of <see cref="ToPlan"/>: undo the shift, undo the scale, turn the other
    /// way. Worth being the inverse rather than a second fit — solving world-from-plan separately
    /// would give two transforms that disagree slightly, and a dot on the board that does not
    /// quite match the circle on the ground is exactly the sort of thing that makes someone
    /// distrust both.
    /// </remarks>
    public Vector2 ToWorld(Vector2 plan)
    {
        if (!IsValid || Scale <= 0f)
            return Vector2.Zero;

        var shifted = (plan - Offset) / Scale;

        return new Vector2(
            (shifted.X * Cos) + (shifted.Y * Sin),
            (shifted.Y * Cos) - (shifted.X * Sin));
    }

    /// <summary>Drops the height axis. FFXIV's ground plane is X and Z; Y is up.</summary>
    public static Vector2 Ground(Vector3 position) => new(position.X, position.Z);

    /// <summary>
    /// Least-squares fit of rotation, scale and shift over the matched waymarks.
    /// </summary>
    /// <remarks>
    /// The closed form: rotate by the angle of the summed cross and dot products of the centred
    /// point sets, scale by their ratio. No iteration and no failure mode beyond the degenerate
    /// one, which is every waymark on the same spot and is checked for.
    /// </remarks>
    public static bool TrySolve(IReadOnlyList<AlignmentPair> pairs, out WorldAlignment alignment)
    {
        alignment = default;

        if (pairs.Count < MinimumPairs)
            return false;

        var worldMean = Vector2.Zero;
        var planMean = Vector2.Zero;

        foreach (var pair in pairs)
        {
            worldMean += pair.World;
            planMean += pair.Plan;
        }

        worldMean /= pairs.Count;
        planMean /= pairs.Count;

        float dot = 0f, cross = 0f, spread = 0f;

        foreach (var pair in pairs)
        {
            var a = pair.World - worldMean;
            var b = pair.Plan - planMean;

            dot += (a.X * b.X) + (a.Y * b.Y);
            cross += (a.X * b.Y) - (a.Y * b.X);
            spread += (a.X * a.X) + (a.Y * a.Y);
        }

        // Every marker on top of every other one. No orientation to recover.
        if (spread < 0.0001f)
            return false;

        var magnitude = MathF.Sqrt((dot * dot) + (cross * cross));
        if (magnitude < 1e-9f)
            return false;

        var scale = magnitude / spread;
        var cos = dot / magnitude;
        var sin = cross / magnitude;

        var offset = new Vector2(
            planMean.X - (scale * ((worldMean.X * cos) - (worldMean.Y * sin))),
            planMean.Y - (scale * ((worldMean.X * sin) + (worldMean.Y * cos))));

        var candidate = new WorldAlignment(scale, sin, cos, offset, 0f);

        var error = 0f;
        foreach (var pair in pairs)
            error += (candidate.ToPlan(pair.World) - pair.Plan).Length();

        alignment = new WorldAlignment(scale, sin, cos, offset, error / pairs.Count);
        return true;
    }
}
