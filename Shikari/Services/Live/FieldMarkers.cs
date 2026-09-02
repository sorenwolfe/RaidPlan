using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Shikari.Services.Live;

/// <summary>Reads the waymarks the group has actually placed in the duty.</summary>
/// <remarks>
/// Read-only. The plugin never places or clears a marker — it only looks at where they are, the
/// same information the game draws on your screen and minimap already.
/// </remarks>
public static class FieldMarkers
{
    /// <summary>Slot order in the game's own array. A-D then 1-4, and it does not change.</summary>
    public static readonly string[] Letters = { "A", "B", "C", "D", "1", "2", "3", "4" };

    public readonly record struct PlacedMarker(string Letter, Vector2 World);

    /// <summary>
    /// The markers currently down, on the ground plane. Empty when none are placed, which is the
    /// normal case outside a duty and the reason callers must cope with an empty list.
    /// </summary>
    public static List<PlacedMarker> Read()
    {
        var found = new List<PlacedMarker>(Letters.Length);

        try
        {
            unsafe
            {
                var controller = MarkingController.Instance();
                if (controller == null)
                    return found;

                var markers = controller->FieldMarkers;

                for (var i = 0; i < Letters.Length && i < markers.Length; i++)
                {
                    var marker = markers[i];
                    if (!marker.Active)
                        continue;

                    found.Add(new PlacedMarker(Letters[i], WorldAlignment.Ground(marker.Position)));
                }
            }
        }
        catch (Exception ex)
        {
            // Game memory. If the layout ever moves under us the feature goes quiet rather than
            // taking the render thread down with it.
            Plugin.Log.Warning(ex, "Could not read the field markers.");
            found.Clear();
        }

        return found;
    }
}
