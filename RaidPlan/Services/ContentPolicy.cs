using RaidPlan.Model;

namespace RaidPlan.Services;

/// <summary>
/// Decides whether the mini plan belongs on screen in the content the player is currently in.
/// Kept free of game types so the rules can be tested without a client attached.
/// </summary>
public static class ContentPolicy
{
    // ContentType row ids. Raids covers normal, alliance and savage tiers; ultimates sit in
    // their own row. HighEndDuty is the belt-and-braces check — every savage and ultimate sets
    // it, so a content type we have wrong still lands in the right place.
    public const uint ContentTypeRaids = 5;
    public const uint ContentTypeUltimate = 28;

    public static bool IsRaidContent(uint contentTypeId, bool highEndDuty) =>
        highEndDuty || contentTypeId is ContentTypeRaids or ContentTypeUltimate;

    /// <summary>
    /// Whether the mini plan should be drawing right now.
    /// </summary>
    /// <param name="mode">What the player asked for in settings.</param>
    /// <param name="manuallyOpen">Set by /raidplan mini, and cleared when they close the window.</param>
    /// <param name="boundByDuty">True inside any instance.</param>
    /// <param name="contentTypeId">ContentType row of the current duty, 0 outside one.</param>
    /// <param name="highEndDuty">Savage or ultimate.</param>
    /// <param name="inCombat">Whether a pull is running.</param>
    /// <param name="hasPlan">False when there is nothing to draw.</param>
    public static bool ShouldShow(
        MiniPlanVisibility mode,
        bool manuallyOpen,
        bool boundByDuty,
        uint contentTypeId,
        bool highEndDuty,
        bool inCombat,
        bool hasPlan)
    {
        if (!hasPlan)
            return false;

        // Opening it by hand works anywhere, including out in the world while building a plan.
        if (manuallyOpen)
            return true;

        if (!boundByDuty)
            return false;

        return mode switch
        {
            MiniPlanVisibility.RaidContent => IsRaidContent(contentTypeId, highEndDuty),
            MiniPlanVisibility.AnyDuty => true,
            MiniPlanVisibility.InCombatOnly => inCombat,
            _ => false,
        };
    }

    /// <summary>
    /// Whether the window should ignore the mouse. During a pull it always does, so a click
    /// aimed at the game can never land on the overlay instead.
    /// </summary>
    public static bool ShouldIgnoreMouse(bool inCombat, bool unlocked) => inCombat && !unlocked;
}
