using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shikari.Services.Live;

/// <summary>
/// Turns the waymarks placed in a duty into waymarks on the board.
/// </summary>
/// <remarks>
/// Two things come of this. The obvious one is not hand-placing eight markers to match a screenshot.
/// The quieter one matters more: a plan whose waymarks came from the real arena lines up with it
/// exactly, which is what lets <see cref="ArenaTracker"/> put live positions on the board at all.
/// </remarks>
public static class WaymarkCapture
{
    /// <summary>
    /// How much of the arena the captured ring covers, by default.
    /// </summary>
    /// <remarks>
    /// The real distances are kept in proportion — only the overall size is a choice, because the
    /// file says nothing about how big the platform is. Most rings sit a little inside the edge.
    /// </remarks>
    public const float DefaultSpread = 0.85f;

    public readonly record struct Placed(string Letter, Vector2 Board);

    /// <summary>
    /// Lays the markers out on the board, keeping their shape and their bearing.
    /// </summary>
    /// <remarks>
    /// No rotation is applied. The game's X runs east and its Z runs south, and the board's axes
    /// run the same way, so a plan captured this way comes out north-up like the game's map — and
    /// a player reading it does not have to rotate anything in their head.
    /// </remarks>
    /// <param name="markers">Markers as placed, on the ground plane.</param>
    /// <param name="arenaEdge">Board radius the outermost marker should land on.</param>
    public static List<Placed> Layout(IReadOnlyList<FieldMarkers.PlacedMarker> markers, float arenaEdge)
    {
        var result = new List<Placed>(markers.Count);
        if (markers.Count == 0)
            return result;

        var centre = Vector2.Zero;
        foreach (var marker in markers)
            centre += marker.World;
        centre /= markers.Count;

        var reach = 0f;
        foreach (var marker in markers)
            reach = MathF.Max(reach, (marker.World - centre).Length());

        // Every marker in one spot. Keeping them stacked is honest; spreading them would invent
        // a layout the arena does not have.
        var scale = reach > 0.01f ? Math.Clamp(arenaEdge, 0.05f, 0.5f) / reach : 0f;
        var middle = new Vector2(0.5f, 0.5f);

        foreach (var marker in markers)
            result.Add(new Placed(marker.Letter, middle + ((marker.World - centre) * scale)));

        return result;
    }
}
