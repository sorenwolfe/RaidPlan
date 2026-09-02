using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Shikari.Services.Live;
using Shikari.UI.Theme;

namespace Shikari.UI.World;

/// <summary>
/// A circle drawn flat on the arena floor, where the plan wants you to stand.
/// </summary>
/// <remarks>
/// Built out of world points rather than a screen-space circle. A circle drawn at a screen position
/// with a screen radius is a circle from every angle, which is exactly what a mark lying on the
/// ground is not: it should foreshorten to an ellipse as the camera comes down, and sit under
/// people's feet rather than floating over them. Projecting a ring of real positions costs a
/// handful of transforms a frame and is the difference between a mark on the floor and a sticker on
/// the screen.
///
/// It is drawn, never placed. Nothing here touches a waymark or moves anybody; it is the plan the
/// group already wrote, shown where it applies.
/// </remarks>
public static class GroundMarker
{
    /// <summary>Points around a circle on the horizontal plane, in world space.</summary>
    /// <remarks>
    /// Kept apart from the drawing so the shape can be checked without a camera. Enough segments
    /// that the edge reads as a curve at the size these are drawn, and few enough that a dozen of
    /// them on screen costs nothing.
    /// </remarks>
    public static Vector3[] RingPoints(Vector3 centre, float radius, int segments)
    {
        if (segments < 3)
            segments = 3;

        var points = new Vector3[segments];

        for (var i = 0; i < segments; i++)
        {
            var angle = i / (float)segments * MathF.PI * 2f;

            points[i] = new Vector3(
                centre.X + (MathF.Cos(angle) * radius),
                centre.Y,
                centre.Z + (MathF.Sin(angle) * radius));
        }

        return points;
    }

    /// <summary>
    /// Draws the spot. False means the camera could not place it, so nothing was drawn.
    /// </summary>
    /// <remarks>
    /// All of it or none of it. A ring with some points behind the camera projects the rest to
    /// mirrored nonsense across the screen, which is far more alarming than a marker that quietly
    /// is not there while you are looking the other way.
    /// </remarks>
    public static bool Draw(Vector3 centre, float radius, bool satisfied, float pulse, uint tint)
    {
        if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            return false;

        if (!Plugin.GameGui.WorldToScreen(centre, out var middle))
            return false;

        var reach = StandingSpot.GlowReach(satisfied, pulse);

        if (!TryProject(centre, radius, out var ring) ||
            !TryProject(centre, radius * reach, out var halo))
        {
            return false;
        }

        var draw = ImGui.GetBackgroundDrawList();

        // Outward first, so the soft edge sits under the solid one rather than over it.
        var glow = StandingSpot.FillAlpha(satisfied, pulse) * 0.45f;
        Fan(draw, middle, halo, Palette.Pack(tint, glow * 0.5f));
        Fan(draw, middle, ring, Palette.Pack(tint, StandingSpot.FillAlpha(satisfied, pulse)));

        var ringColour = Palette.Pack(tint, StandingSpot.RingAlpha(satisfied, pulse));
        var thickness = (satisfied ? 3.2f : 2.2f) * UiHelpers.Scale;

        for (var i = 0; i < ring.Length; i++)
            draw.AddLine(ring[i], ring[(i + 1) % ring.Length], ringColour, thickness);

        // A small bright centre once the spot is met: it reads as a pin dropped on the floor and
        // removes any doubt about which circle is yours when two overlap.
        if (satisfied)
            draw.AddCircleFilled(middle, 3.5f * UiHelpers.Scale, Palette.Pack(tint, 0.95f));

        return true;
    }

    private static bool TryProject(Vector3 centre, float radius, out Vector2[] screen)
    {
        var world = RingPoints(centre, radius, 48);
        screen = new Vector2[world.Length];

        for (var i = 0; i < world.Length; i++)
        {
            if (!Plugin.GameGui.WorldToScreen(world[i], out screen[i]))
                return false;
        }

        return true;
    }

    private static void Fan(ImDrawListPtr draw, Vector2 middle, Vector2[] ring, uint colour)
    {
        for (var i = 0; i < ring.Length; i++)
            draw.AddTriangleFilled(middle, ring[i], ring[(i + 1) % ring.Length], colour);
    }
}
