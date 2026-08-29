using System;
using System.Collections.Generic;
using System.Linq;
using RaidPlan.Model;

namespace RaidPlan.Services;

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
    public int ResolveLocalSlot(RaidPlanDocument? plan)
    {
        if (plan == null || plan.Roster.Count == 0)
            return -1;

        var team = Plugin.Config.GetActiveTeam();
        if (team.PinnedSlotIndex >= 0 && team.PinnedSlotIndex < plan.Roster.Count)
            return team.PinnedSlotIndex;

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null)
            return -1;

        var name = local.Name.TextValue;
        for (var i = 0; i < plan.Roster.Count; i++)
        {
            if (string.Equals(plan.Roster[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        // Nobody matched by name. Fall back to the only free seat matching our job, if there
        // is exactly one — that covers a fresh plan where names were never filled in.
        var jobId = local.ClassJob.RowId;
        var candidates = new List<int>();
        for (var i = 0; i < plan.Roster.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(plan.Roster[i].Name) && plan.Roster[i].JobId == jobId)
                candidates.Add(i);
        }

        return candidates.Count == 1 ? candidates[0] : -1;
    }

    /// <summary>
    /// Writes the current party into the plan's roster. Seats already holding one of the
    /// present players keep their position, so an existing plan is not shuffled.
    /// </summary>
    public int FillFromParty(RaidPlanDocument plan)
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

    private static int? FindSeat(RaidPlanDocument plan, bool[] used, RaidRole? role, bool requireEmpty)
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
