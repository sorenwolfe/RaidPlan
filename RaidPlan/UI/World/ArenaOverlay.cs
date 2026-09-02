using System;
using System.Linq;
using System.Numerics;
using RaidPlan.Model;
using RaidPlan.Services.Live;
using RaidPlan.UI.Theme;

namespace RaidPlan.UI.World;

/// <summary>
/// Puts your own spot from the current slide onto the arena floor.
/// </summary>
/// <remarks>
/// The gap this closes is the last one left. The plan says north-east tower, the mini board shows
/// it, and there is still a step where a player turns a picture into a direction while something is
/// casting at them. Drawing it on the ground removes that step: the answer is already where the
/// answer goes.
///
/// Deliberately only your own spot. Eight circles is a diagram on the floor and hides the arena;
/// one is a hint. Everyone else's positions stay on the mini board, where looking at them is a
/// choice rather than something that happens whenever you face that way.
///
/// The whole thing is off unless the waymarks line up well enough to trust the fit. Being told
/// confidently to stand in the wrong place is far worse than being told nothing, and the residual
/// from the alignment is exactly the number that knows the difference.
/// </remarks>
public sealed class ArenaOverlay : IDisposable
{
    /// <summary>Smallest circle worth drawing, in yalms.</summary>
    /// <remarks>A spot tighter than this is asking for pixel-accurate standing, which no plan means.</remarks>
    public const float MinimumRadius = 0.75f;

    /// <summary>Largest, so a mistyped setting cannot paint the whole arena gold.</summary>
    public const float MaximumRadius = 12f;

    private bool satisfied;

    public ArenaOverlay() => Plugin.PluginInterface.UiBuilder.Draw += Draw;

    /// <summary>Whether the player was standing in their spot on the last frame that drew one.</summary>
    public bool IsSatisfied => satisfied;

    /// <summary>Why nothing is being drawn, in words that say what to do about it.</summary>
    public string Status { get; private set; } = "off";

    /// <summary>Clamps the configured radius into something that can sensibly be stood in.</summary>
    public static float Radius(float configured) =>
        Math.Clamp(float.IsNaN(configured) ? MinimumRadius : configured, MinimumRadius, MaximumRadius);

    public void Dispose() => Plugin.PluginInterface.UiBuilder.Draw -= Draw;

    private void Draw()
    {
        try
        {
            if (!Plugin.Config.ShowArenaSpot)
            {
                Status = "off";
                satisfied = false;
                return;
            }

            if (!TryFindSpot(out var target, out var reason))
            {
                Status = reason;
                satisfied = false;
                return;
            }

            var me = Plugin.ObjectTable.LocalPlayer;
            if (me == null)
            {
                Status = "not in the world";
                satisfied = false;
                return;
            }

            var radius = Radius(Plugin.Config.ArenaSpotYalms);
            var flat = new Vector2(me.Position.X - target.X, me.Position.Z - target.Z);

            satisfied = StandingSpot.IsSatisfied(satisfied, flat.Length(), radius);

            var pulse = StandingSpot.Pulse((float)Plugin.Framework.LastUpdateUTC.TimeOfDay.TotalSeconds);
            var tint = satisfied ? Palette.Attention : Palette.Attention;

            Status = GroundMarker.Draw(target, radius, satisfied, pulse, tint)
                ? satisfied ? "standing in it" : "drawn"
                : "behind the camera";
        }
        catch (Exception ex)
        {
            // Once a frame. A throw here would land in the middle of Dalamud's draw loop.
            Status = "failed";
            Plugin.Log.Error(ex, "RaidPlan: could not draw the arena spot.");
        }
    }

    /// <summary>Where the plan wants the local player to be, in world coordinates.</summary>
    private static bool TryFindSpot(out Vector3 target, out string reason)
    {
        target = default;

        var plan = Plugin.Plans.Active;
        if (plan == null || plan.Slides.Count == 0)
        {
            reason = "no plan loaded";
            return false;
        }

        var slide = plan.Slides[Math.Clamp(Plugin.Main.SlideIndex, 0, plan.Slides.Count - 1)];

        var slot = Plugin.Roster.ResolveLocalSlot(plan);
        if (slot < 0)
        {
            reason = "you are not on the roster";
            return false;
        }

        var token = slide.Items.FirstOrDefault(
            item => item.Kind == CanvasItemKind.PlayerToken && item.SlotIndex == slot);

        if (token == null)
        {
            reason = "this slide does not place you";
            return false;
        }

        if (!Plugin.Tracker.TryAlign(plan, slide, out var alignment) || !alignment.IsTrustworthy)
        {
            reason = "waymarks do not line up with the plan";
            return false;
        }

        var ground = alignment.ToWorld(token.Position);

        // The plan is flat and the arena is not, so the height comes from the player rather than
        // from the plan. Every arena a plan is drawn for is level enough that the difference is the
        // step you are standing on.
        var height = Plugin.ObjectTable.LocalPlayer?.Position.Y ?? 0f;

        target = new Vector3(ground.X, height, ground.Y);
        reason = string.Empty;
        return true;
    }
}
