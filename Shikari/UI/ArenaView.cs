using System;
using System.Numerics;

namespace Shikari.UI;

/// <summary>
/// Where the arena widget is looking: how far in, and at what.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="ArenaCanvas"/> so the arithmetic can be exercised without a running
/// ImGui. The canvas turns this into a board origin and side and then draws exactly as it did
/// before zoom existed — nothing downstream of it knows the view has moved.
/// </remarks>
public struct ArenaView
{
    public const float MinZoom = 1f;

    /// <summary>Six is close enough to work on a single token without losing the room around it.</summary>
    public const float MaxZoom = 6f;

    private float zoom = MinZoom;
    private Vector2 focus = new(0.5f, 0.5f);

    public ArenaView()
    {
    }

    public float Zoom => zoom;

    /// <summary>The board point sitting in the middle of the widget.</summary>
    public Vector2 Focus => focus;

    public bool IsZoomedIn => zoom > MinZoom + 0.001f;

    public void Reset()
    {
        zoom = MinZoom;
        focus = new Vector2(0.5f, 0.5f);
    }

    /// <summary>Zooms about the middle of the view, which is what a button press should do.</summary>
    public void SetZoom(float target) => ZoomAbout(target, focus);

    /// <summary>
    /// Zooms while keeping one board point under the same pixel — what anyone expects from a
    /// wheel over a map, and the difference between zooming in on a mechanic and hunting for it
    /// afterwards.
    /// </summary>
    public void ZoomAbout(float target, Vector2 anchor)
    {
        var next = Math.Clamp(target, MinZoom, MaxZoom);
        if (Math.Abs(next - zoom) < 0.0001f)
            return;

        focus = anchor - ((anchor - focus) * (zoom / next));
        zoom = next;
        Clamp();
    }

    /// <summary>Drags the view by a distance in widget pixels.</summary>
    public void Pan(Vector2 pixels, float viewSide)
    {
        if (viewSide <= 0f)
            return;

        focus -= pixels / (viewSide * zoom);
        Clamp();
    }

    /// <summary>The whole board's side in pixels, given the widget's.</summary>
    public readonly float BoardSide(float viewSide) => viewSide * zoom;

    /// <summary>Top-left of the whole board in screen space. Off the widget once zoomed in.</summary>
    public readonly Vector2 BoardOrigin(Vector2 viewOrigin, float viewSide) =>
        viewOrigin + (new Vector2(viewSide, viewSide) * 0.5f) - (focus * BoardSide(viewSide));

    /// <summary>
    /// Keeps the board covering the widget, so the arena can never be panned off the edge and
    /// left as an empty square with no obvious way back.
    /// </summary>
    private void Clamp()
    {
        var half = 0.5f / zoom;
        focus = new Vector2(
            Math.Clamp(focus.X, half, 1f - half),
            Math.Clamp(focus.Y, half, 1f - half));
    }
}
