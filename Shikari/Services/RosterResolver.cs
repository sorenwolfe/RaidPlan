using System;
using System.Collections.Generic;
using System.Linq;
using Shikari.Model;

namespace Shikari.Services;

/// <summary>
/// Connects the plan's abstract seats to the people actually in the party, and works out
/// which seat this client is sitting in so calls can be addressed correctly.
/// </summary>
public sealed class RosterResolver
{
    /// <summary>
    /// Index of the seat the local player occupies, or -1 when it cannot be determined.
    /// A pinned seat on the active team profile always wins.
    /// </summary>
    public int ResolveLocalSlot(PlanDocument? plan)
    {
        if (plan == null || plan.Roster.Count == 0)
            return -1;

        var local = Plugin.ObjectTable.LocalPlayer;

        return MatchSeat(
            plan.Roster,
            local?.Name.TextValue ?? string.Empty,
            local?.ClassJob.RowId ?? 0,
            Plugin.Config.GetActiveTeam().PinnedSlotIndex);
    }

    /// <summary>
    /// Works out which seat a player is sitting in. Split out from the game lookup so the rules
    /// can be tested.
    /// </summary>
    /// <remarks>
    /// In order: a seat the player pinned by hand, then their character name, then their job.
    /// The job passes only when exactly one seat could be meant — with two Summoners on the board
    /// there is no way to tell which is you, and guessing would highlight the wrong person.
    /// </remarks>
    public static int MatchSeat(IReadOnlyList<PlayerSlot> roster, string localName, uint localJobId, int pinnedIndex)
    {
        if (roster.Count == 0)
            return -1;

        if (pinnedIndex >= 0 && pinnedIndex < roster.Count)
            return pinnedIndex;

        if (!string.IsNullOrEmpty(localName))
        {
            for (var i = 0; i < roster.Count; i++)
            {
                if (string.Equals(roster[i].Name, localName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        if (localJobId == 0)
            return -1;

        // An unnamed seat of our job first: a plan drawn before anyone filled the names in.
        var free = Only(roster, i => string.IsNullOrWhiteSpace(roster[i].Name) && roster[i].JobId == localJobId);
        if (free >= 0)
            return free;

        // Then any seat of our job. This is the case that matters in practice — a plan built by
        // someone else, with their static's names on it, opened by a player whose job appears once.
        return Only(roster, i => roster[i].JobId == localJobId);
    }

    /// <summary>Index of the only seat matching, or -1 when none or several do.</summary>
    private static int Only(IReadOnlyList<PlayerSlot> roster, Func<int, bool> predicate)
    {
        var found = -1;
        for (var i = 0; i < roster.Count; i++)
        {
            if (!predicate(i))
                continue;
            if (found >= 0)
                return -1;
            found = i;
        }

        return found;
    }

    /// <summary>
    /// Writes the current party into the plan's roster. Seats already holding one of the
    /// present players keep their position, so an existing plan is not shuffled.
    /// </summary>
    public int FillFromParty(PlanDocument plan)
    {
        var members = ReadParty();
        if (members.Count == 0)
            return 0;

        var used = new bool[plan.Roster.Count];
        var placed = 0;

        // First pass: anyone already named in the roster stays where they are.
        foreach (var member in members.ToList())
        {
            for (var i = 0; i < plan.Roster.Count; i++)
            {
                if (used[i])
                    continue;
                if (!string.Equals(plan.Roster[i].Name, member.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                ApplyMember(plan.Roster[i], member);
                used[i] = true;
                members.Remove(member);
                placed++;
                break;
            }
        }

        // Second pass: prefer an empty seat of the right role, then any empty seat, and only
        // fall back to overwriting a named seat once nothing else is free.
        foreach (var member in members)
        {
            var target = FindSeat(plan, used, member.Role, requireEmpty: true)
                         ?? FindSeat(plan, used, null, requireEmpty: true)
                         ?? FindSeat(plan, used, member.Role, requireEmpty: false)
                         ?? FindSeat(plan, used, null, requireEmpty: false);
            if (target == null)
                break;

            ApplyMember(plan.Roster[target.Value], member);
            used[target.Value] = true;
            placed++;
        }

        return placed;
    }

    private static int? FindSeat(PlanDocument plan, bool[] used, RaidRole? role, bool requireEmpty)
    {
        for (var i = 0; i < plan.Roster.Count; i++)
        {
            if (used[i])
                continue;
            if (role.HasValue && plan.Roster[i].Role != role.Value)
                continue;
            if (requireEmpty && !string.IsNullOrWhiteSpace(plan.Roster[i].Name))
                continue;
            return i;
        }

        return null;
    }

    private static void ApplyMember(PlayerSlot slot, PartyMemberInfo member)
    {
        slot.Name = member.Name;
        slot.JobId = member.JobId;
        if (member.Role != RaidRole.Unknown)
            slot.Role = member.Role;
        if (slot.Color == 0)
            slot.Color = RoleColors.Default(slot.Role);
    }

    public readonly record struct PartyMemberInfo(string Name, uint JobId, RaidRole Role);

    /// <summary>Reads the current party, or just the local player when solo.</summary>
    public List<PartyMemberInfo> ReadParty()
    {
        var result = new List<PartyMemberInfo>();

        if (Plugin.PartyList.Length > 0)
        {
            for (var i = 0; i < Plugin.PartyList.Length; i++)
            {
                var member = Plugin.PartyList[i];
                if (member == null)
                    continue;

                var jobId = member.ClassJob.RowId;
                result.Add(new PartyMemberInfo(
                    member.Name.TextValue,
                    jobId,
                    JobRoles.RoleFor(Plugin.Actions.JobAbbreviation(jobId))));
            }

            return result;
        }

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local != null)
        {
            var jobId = local.ClassJob.RowId;
            result.Add(new PartyMemberInfo(
                local.Name.TextValue,
                jobId,
                JobRoles.RoleFor(Plugin.Actions.JobAbbreviation(jobId))));
        }

        return result;
    }
}
