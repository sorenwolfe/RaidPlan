using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using RaidPlan.Model;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace RaidPlan.Services;

/// <summary>A single searchable action pulled out of the game's Action sheet.</summary>
public sealed class ActionEntry
{
    public uint RowId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SearchName { get; init; } = string.Empty;
    public ushort IconId { get; init; }
    public uint ClassJobId { get; init; }
    public uint CategoryId { get; init; }
    public bool IsRoleAction { get; init; }
    public bool IsPlayerAction { get; init; }
    public byte Level { get; init; }
    public float RecastSeconds { get; init; }
    public string JobAbbreviation { get; init; } = string.Empty;
}

/// <summary>Minimal job record used to colour and filter the spell picker.</summary>
public sealed class JobEntry
{
    public uint RowId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Abbreviation { get; init; } = string.Empty;
    public RaidRole Role { get; init; }
    public bool IsCombatJob { get; init; }
}

/// <summary>
/// Builds and searches an in-memory index of the game's actions. Two views are kept: the
/// player-usable actions offered by the assignment picker, and every named action, which is
/// what the timeline needs when you anchor a step to a boss cast.
/// </summary>
public sealed class ActionIndex
{
    private readonly List<ActionEntry> playerActions = new();
    private readonly List<ActionEntry> allActions = new();
    private readonly Dictionary<uint, ActionEntry> byId = new();
    private readonly Dictionary<uint, JobEntry> jobsById = new();

    /// <summary>Category row id to the set of ClassJob row ids it covers.</summary>
    private readonly Dictionary<uint, HashSet<uint>> categoryJobs = new();

    public bool Ready { get; private set; }

    public IReadOnlyList<JobEntry> Jobs { get; private set; } = Array.Empty<JobEntry>();

    /// <summary>Kick off the index build on a worker thread; the UI stays responsive meanwhile.</summary>
    public Task BuildAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                Build();
                Ready = true;
                Plugin.Log.Information(
                    "Action index ready: {Player} player actions, {All} total, {Jobs} jobs.",
                    playerActions.Count, allActions.Count, jobsById.Count);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to build the action index.");
            }
        });
    }

    private void Build()
    {
        BuildJobs();

        var actionSheet = Plugin.DataManager.GetExcelSheet<LuminaAction>();
        if (actionSheet == null)
            return;

        foreach (var row in actionSheet)
        {
            if (row.RowId == 0)
                continue;

            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var jobId = row.ClassJob.RowId;
            // ClassJob is stored as -1 (0xFFFFFFFF) for actions with no owning job.
            if (jobId == uint.MaxValue)
                jobId = 0;

            var isPlayerAction = !row.IsPvP && (row.ClassJobLevel > 0 || row.IsRoleAction);

            var entry = new ActionEntry
            {
                RowId = row.RowId,
                Name = name,
                SearchName = name.ToLowerInvariant(),
                IconId = row.Icon,
                ClassJobId = jobId,
                CategoryId = row.ClassJobCategory.RowId,
                IsRoleAction = row.IsRoleAction,
                IsPlayerAction = isPlayerAction,
                Level = row.ClassJobLevel,
                RecastSeconds = row.Recast100ms / 10f,
                JobAbbreviation = jobsById.TryGetValue(jobId, out var job) ? job.Abbreviation : string.Empty,
            };

            allActions.Add(entry);
            byId[entry.RowId] = entry;

            if (isPlayerAction)
                playerActions.Add(entry);
        }

        allActions.Sort(static (a, b) => string.CompareOrdinal(a.SearchName, b.SearchName));
        playerActions.Sort(static (a, b) => string.CompareOrdinal(a.SearchName, b.SearchName));

        BuildCategoryMap();
    }

    private void BuildJobs()
    {
        var sheet = Plugin.DataManager.GetExcelSheet<ClassJob>();
        if (sheet == null)
            return;

        var list = new List<JobEntry>();
        foreach (var row in sheet)
        {
            if (row.RowId == 0)
                continue;

            var abbr = row.Abbreviation.ExtractText();
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(abbr) || string.IsNullOrWhiteSpace(name))
                continue;

            var entry = new JobEntry
            {
                RowId = row.RowId,
                Name = Capitalise(name),
                Abbreviation = abbr.ToUpperInvariant(),
                Role = JobRoles.RoleFor(abbr.ToUpperInvariant()),
                IsCombatJob = JobRoles.RoleFor(abbr.ToUpperInvariant()) != RaidRole.Unknown,
            };

            jobsById[entry.RowId] = entry;
            list.Add(entry);
        }

        Jobs = list
            .OrderByDescending(j => j.IsCombatJob)
            .ThenBy(j => (int)j.Role)
            .ThenBy(j => j.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// ClassJobCategory exposes one boolean column per job abbreviation. Reading them once at
    /// startup gives a cheap "can job X use category Y" lookup for the rest of the session.
    /// </summary>
    private void BuildCategoryMap()
    {
        var sheet = Plugin.DataManager.GetExcelSheet<ClassJobCategory>();
        if (sheet == null)
            return;

        var accessors = new List<(uint JobId, PropertyInfo Property)>();
        foreach (var job in jobsById.Values)
        {
            var prop = typeof(ClassJobCategory).GetProperty(
                job.Abbreviation,
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(bool))
                accessors.Add((job.RowId, prop));
        }

        foreach (var row in sheet)
        {
            var set = new HashSet<uint>();
            object boxed = row;
            foreach (var (jobId, prop) in accessors)
            {
                try
                {
                    if (prop.GetValue(boxed) is true)
                        set.Add(jobId);
                }
                catch
                {
                    // A malformed row should not take the whole index down.
                }
            }

            categoryJobs[row.RowId] = set;
        }
    }

    public ActionEntry? Get(uint actionId) => byId.GetValueOrDefault(actionId);

    public string NameOf(uint actionId, string fallback = "")
    {
        var entry = Get(actionId);
        if (entry != null)
            return entry.Name;
        return string.IsNullOrEmpty(fallback) ? $"Action #{actionId}" : fallback;
    }

    public JobEntry? Job(uint jobId) => jobsById.GetValueOrDefault(jobId);

    public string JobAbbreviation(uint jobId) => jobsById.TryGetValue(jobId, out var j) ? j.Abbreviation : "???";

    public bool CanJobUse(ActionEntry entry, uint jobId)
    {
        if (jobId == 0)
            return true;
        if (entry.ClassJobId == jobId)
            return true;
        if (categoryJobs.TryGetValue(entry.CategoryId, out var jobs))
            return jobs.Contains(jobId);
        return false;
    }

    /// <summary>
    /// Searches player-usable actions, optionally narrowed to one job. Exact and prefix matches
    /// are ranked above matches buried in the middle of a name.
    /// </summary>
    public List<ActionEntry> SearchPlayerActions(string query, uint jobId, int limit = 60)
    {
        return Search(playerActions, query, jobId, limit);
    }

    /// <summary>Searches every named action, which is what boss casts live in.</summary>
    public List<ActionEntry> SearchAllActions(string query, int limit = 60)
    {
        return Search(allActions, query, 0, limit);
    }

    private List<ActionEntry> Search(List<ActionEntry> source, string query, uint jobId, int limit)
    {
        if (!Ready)
            return new List<ActionEntry>();

        var q = (query ?? string.Empty).Trim().ToLowerInvariant();
        var results = new List<(int Rank, ActionEntry Entry)>();

        foreach (var entry in source)
        {
            if (jobId != 0 && !CanJobUse(entry, jobId))
                continue;

            int rank;
            if (q.Length == 0)
            {
                rank = 3;
            }
            else if (entry.SearchName == q)
            {
                rank = 0;
            }
            else if (entry.SearchName.StartsWith(q, StringComparison.Ordinal))
            {
                rank = 1;
            }
            else if (entry.SearchName.Contains(q, StringComparison.Ordinal))
            {
                rank = 2;
            }
            else
            {
                continue;
            }

            results.Add((rank, entry));
            if (q.Length == 0 && results.Count >= limit * 4)
                break;
        }

        return results
            .OrderBy(r => r.Rank)
            .ThenBy(r => r.Entry.Name.Length)
            .ThenBy(r => r.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(r => r.Entry)
            .ToList();
    }

    private static string Capitalise(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}

/// <summary>Maps job abbreviations onto the roles a raid plan cares about.</summary>
public static class JobRoles
{
    private static readonly HashSet<string> Tanks = new(StringComparer.OrdinalIgnoreCase)
        { "PLD", "WAR", "DRK", "GNB", "GLA", "MRD" };

    private static readonly HashSet<string> Healers = new(StringComparer.OrdinalIgnoreCase)
        { "WHM", "SCH", "AST", "SGE", "CNJ" };

    private static readonly HashSet<string> Melee = new(StringComparer.OrdinalIgnoreCase)
        { "MNK", "DRG", "NIN", "SAM", "RPR", "VPR", "PGL", "LNC", "ROG" };

    private static readonly HashSet<string> PhysRanged = new(StringComparer.OrdinalIgnoreCase)
        { "BRD", "MCH", "DNC", "ARC" };

    private static readonly HashSet<string> MagRanged = new(StringComparer.OrdinalIgnoreCase)
        { "BLM", "SMN", "RDM", "PCT", "BLU", "THM", "ACN" };

    public static RaidRole RoleFor(string abbreviation)
    {
        if (Tanks.Contains(abbreviation)) return RaidRole.Tank;
        if (Healers.Contains(abbreviation)) return RaidRole.Healer;
        if (Melee.Contains(abbreviation)) return RaidRole.Melee;
        if (PhysRanged.Contains(abbreviation)) return RaidRole.PhysicalRanged;
        if (MagRanged.Contains(abbreviation)) return RaidRole.MagicalRanged;
        return RaidRole.Unknown;
    }
}
