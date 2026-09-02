namespace Shikari.Services;

/// <summary>
/// Decides which of a job's actions are worth putting on a raid plan.
/// </summary>
/// <remarks>
/// A GCD is 2.5 seconds, so a recast of 30 seconds or more is a cooldown rather than rotation.
/// That single line covers mitigation, healing cooldowns, raid buffs and burst windows while
/// dropping the hundred-odd filler actions nobody assigns. Role actions come in regardless:
/// Reprisal, Feint, Addle, Esuna and friends are the whole point.
/// </remarks>
public static class ActionFilter
{
    /// <summary>Recast at or above this is treated as a cooldown. A GCD sits at 2.5s.</summary>
    public const float CooldownRecastSeconds = 30f;

    public static bool IsCooldown(float recastSeconds, bool isRoleAction, bool isPlayerAction) =>
        isPlayerAction && (recastSeconds >= CooldownRecastSeconds || isRoleAction);
}
