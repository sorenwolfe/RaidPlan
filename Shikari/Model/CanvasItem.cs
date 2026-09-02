using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;

namespace Shikari.Model;

/// <summary>
/// One object on a slide.
/// </summary>
/// <remarks>
/// Coordinates are normalised to the arena box, so a plan looks the same at any window size.
/// The ShouldSerialize* methods below keep share codes small by dropping fields a given kind
/// can't use.
/// </remarks>
public sealed class CanvasItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [DefaultValue(CanvasItemKind.PlayerToken)]
    public CanvasItemKind Kind { get; set; } = CanvasItemKind.PlayerToken;

    /// <summary>Centre of the item, normalised.</summary>
    public Vector2 Position { get; set; } = new(0.5f, 0.5f);

    /// <summary>Half-extents for rectangles, normalised. Ignored by most kinds.</summary>
    public Vector2 Extent { get; set; } = new(0.1f, 0.1f);

    /// <summary>Radius for circular kinds, normalised against arena width.</summary>
    [DefaultValue(0.12f)]
    public float Radius { get; set; } = 0.12f;

    /// <summary>Inner radius for donuts, normalised.</summary>
    [DefaultValue(0.06f)]
    public float InnerRadius { get; set; } = 0.06f;

    /// <summary>Facing, in degrees clockwise from north. Used by cones, rectangles and arrows.</summary>
    [DefaultValue(0f)]
    public float Rotation { get; set; }

    /// <summary>Total sweep of a cone, in degrees.</summary>
    [DefaultValue(90f)]
    public float ConeAngle { get; set; } = 90f;

    /// <summary>Packed ABGR colour, matching what ImGui's draw list expects.</summary>
    [DefaultValue(0xFF4FA3FFu)]
    public uint Color { get; set; } = 0xFF4FA3FF;

    /// <summary>Free text: the label body, the waymark letter, or a token's caption.</summary>
    [DefaultValue("")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Roster slot this token is bound to, or -1 for an unbound token.</summary>
    [DefaultValue(-1)]
    public int SlotIndex { get; set; } = -1;

    /// <summary>Optional game icon drawn inside the token.</summary>
    [DefaultValue(0u)]
    public uint IconId { get; set; }

    [DefaultValue(ZoneShape.Circle)]
    public ZoneShape Zone { get; set; } = ZoneShape.Circle;

    /// <summary>Path points for arrows, tethers and freehand strokes. Normalised.</summary>
    public List<Vector2> Points { get; set; } = new();

    /// <summary>Stroke width in normalised units for freehand and tethers.</summary>
    [DefaultValue(0.006f)]
    public float Thickness { get; set; } = 0.006f;

    /// <summary>Higher layers draw on top.</summary>
    [DefaultValue(0)]
    public int Layer { get; set; }

    /// <summary>Locked items cannot be selected or dragged.</summary>
    [DefaultValue(false)]
    public bool Locked { get; set; }

    private bool IsPath => Kind is CanvasItemKind.Arrow or CanvasItemKind.Tether or CanvasItemKind.Freehand;

    private bool IsToken => Kind is CanvasItemKind.PlayerToken or CanvasItemKind.EnemyToken or CanvasItemKind.Waymark;

    // Newtonsoft picks these up by convention.

    // Ids only matter within a session, so don't pay 32 characters each for them in a share code.
    public bool ShouldSerializeId() => false;

    public bool ShouldSerializePosition() => !IsPath;

    public bool ShouldSerializeExtent() =>
        Kind == CanvasItemKind.Zone && Zone is ZoneShape.Rectangle or ZoneShape.Line or ZoneShape.Cross;

    public bool ShouldSerializeRadius() => IsToken || Kind == CanvasItemKind.Zone;

    public bool ShouldSerializeInnerRadius() => Kind == CanvasItemKind.Zone && Zone == ZoneShape.Donut;

    public bool ShouldSerializeConeAngle() => Kind == CanvasItemKind.Zone && Zone == ZoneShape.Cone;

    public bool ShouldSerializeZone() => Kind == CanvasItemKind.Zone;

    public bool ShouldSerializePoints() => Points.Count > 0;

    public bool ShouldSerializeThickness() => IsPath;

    public bool ShouldSerializeSlotIndex() => Kind == CanvasItemKind.PlayerToken;

    public CanvasItem Clone()
    {
        return new CanvasItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = Kind,
            Position = Position,
            Extent = Extent,
            Radius = Radius,
            InnerRadius = InnerRadius,
            Rotation = Rotation,
            ConeAngle = ConeAngle,
            Color = Color,
            Text = Text,
            SlotIndex = SlotIndex,
            IconId = IconId,
            Zone = Zone,
            Points = new List<Vector2>(Points),
            Thickness = Thickness,
            Layer = Layer,
            Locked = Locked,
        };
    }
}
