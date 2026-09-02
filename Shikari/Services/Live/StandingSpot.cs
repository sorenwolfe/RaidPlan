using System;

namespace Shikari.Services.Live;

/// <summary>
/// Whether the player is standing where the plan wants them, and how that should look.
/// </summary>
/// <remarks>
/// Separated from the drawing because the interesting parts are not the drawing. Standing exactly
/// on the boundary of a circle makes the distance cross it several times a second, and a marker
/// that flips between two appearances at that rate is worse than one that never changes — it draws
/// the eye at the exact moment the player should be watching the boss. The margin below is what
/// stops that, and it is the sort of rule that is easy to leave out and impossible to see in a
/// screenshot.
/// </remarks>
public static class StandingSpot
{
    /// <summary>
    /// How much further than the radius the player may drift before the spot stops counting as met.
    /// </summary>
    /// <remarks>
    /// Entering and leaving at the same distance means a player standing on the line toggles on
    /// every step. Fifteen percent is wide enough to cover that and narrow enough that nobody is
    /// ever told they are in the right place when they are visibly outside the circle.
    /// </remarks>
    public const float ExitMargin = 1.15f;

    /// <summary>How long one breath of the pulse takes, in seconds.</summary>
    /// <remarks>
    /// Slow on purpose. A fast pulse reads as an alarm, and this is a hint that should sit in the
    /// corner of the eye rather than compete with the mechanic it is helping with.
    /// </remarks>
    public const float PulseSeconds = 1.8f;

    /// <summary>
    /// Whether the spot counts as met, given where it stood a moment ago.
    /// </summary>
    /// <remarks>
    /// The previous answer is an input, which is what makes the margin work: the circle is entered
    /// at its edge and left a little further out.
    /// </remarks>
    public static bool IsSatisfied(bool wasSatisfied, float distance, float radius)
    {
        if (radius <= 0f || float.IsNaN(distance) || float.IsInfinity(distance))
            return false;

        return distance <= (wasSatisfied ? radius * ExitMargin : radius);
    }

    /// <summary>A smooth nought-to-one breath. Starts at nought so it fades up, never in.</summary>
    public static float Pulse(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            return 0f;

        var turn = seconds / PulseSeconds * MathF.PI * 2f;
        return 0.5f - (0.5f * MathF.Cos(turn));
    }

    /// <summary>How solid the ground inside the circle is.</summary>
    /// <remarks>
    /// The whole point of the change is that being in the right place should be obvious without
    /// being looked at, so the met state is both brighter and still, and the waiting state is
    /// dimmer and moving. Either one alone is easy to miss in a busy arena.
    /// </remarks>
    public static float FillAlpha(bool satisfied, float pulse)
    {
        pulse = Math.Clamp(pulse, 0f, 1f);

        return satisfied ? 0.30f : 0.10f + (0.07f * pulse);
    }

    /// <summary>How bright the ring around the circle is.</summary>
    public static float RingAlpha(bool satisfied, float pulse)
    {
        pulse = Math.Clamp(pulse, 0f, 1f);

        return satisfied ? 0.95f : 0.45f + (0.35f * pulse);
    }

    /// <summary>
    /// How far past the ring the glow reaches, as a multiple of the radius.
    /// </summary>
    /// <remarks>
    /// The glow breathes outwards while waiting and settles when met, so the movement stops the
    /// moment the player is where they should be. A marker that keeps moving after the job is done
    /// keeps asking for attention it no longer needs.
    /// </remarks>
    public static float GlowReach(bool satisfied, float pulse)
    {
        pulse = Math.Clamp(pulse, 0f, 1f);

        return satisfied ? 1.18f : 1.10f + (0.16f * pulse);
    }
}
