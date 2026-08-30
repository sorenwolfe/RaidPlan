using System.Collections.Generic;

namespace RaidPlan.UI;

/// <summary>One entry in the slide editor's tool palette.</summary>
public sealed record ToolInfo(CanvasTool Tool, string Label, string Tip);

/// <summary>
/// What each drawing tool is called and what it does. Kept apart from the drawing code so the
/// wording is easy to find and to check: a tool with no tooltip is invisible until a player
/// hovers it and gets nothing back.
/// </summary>
public static class ToolCatalog
{
    public static readonly IReadOnlyList<ToolInfo> All = new ToolInfo[]
    {
        new(CanvasTool.Select, "Select",
            "Pick things up and move them about. Click to select, drag to move, right-click for " +
            "duplicate and delete. Anything already on the slide can be nudged with this."),

        new(CanvasTool.PlayerToken, "Player",
            "Places one of your party on the arena. The seat you pick below decides whose token " +
            "it is, and it takes that seat's job colour and icon automatically."),

        new(CanvasTool.EnemyToken, "Enemy",
            "Places the boss, or an add. A marker for the picture only — it isn't tied to " +
            "anything the real boss does."),

        new(CanvasTool.Waymark, "Waymark",
            "Places a field marker, A to D or 1 to 4, drawn the same way as the ones you drop in " +
            "game. Pick which one below."),

        new(CanvasTool.Zone, "AoE",
            "Draws a telegraph: circle, donut, rectangle, cone, line or cross. Pick the shape " +
            "below, then size and turn it on the right after placing it."),

        new(CanvasTool.Arrow, "Arrow",
            "Shows where somebody moves to. Click once where the movement starts, again where " +
            "it ends."),

        new(CanvasTool.Tether, "Tether",
            "Draws a line between two points, for tethers, chains and partner mechanics. Click " +
            "once at each end."),

        new(CanvasTool.Label, "Text",
            "Drops a text label onto the arena, for anything the drawing doesn't say on its own. " +
            "Type the wording on the right once it's placed."),

        new(CanvasTool.Pen, "Pen",
            "Freehand drawing, for arcs and boundaries the other shapes don't cover. Hold the " +
            "left button and drag."),
    };
}
