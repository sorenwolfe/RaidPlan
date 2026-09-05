using System;
using System.Collections.Generic;
using System.Linq;
using Shikari.Model;

namespace Shikari.Services.Adaptive;

public readonly record struct StatusSample(uint Id, float Remaining, ushort Parameter, uint Source);

/// <summary>Absence of a readable actor invalidates the baseline; it never means status loss.</summary>
public sealed class StatusTracker
{
    private Dictionary<(uint, uint), StatusSample> previous = new();
    private bool ready;
    public void Invalidate() { previous.Clear(); ready = false; }
    public List<StatusObservation> Observe(IEnumerable<StatusSample> snapshot, float time)
    {
        var current = new Dictionary<(uint, uint), StatusSample>();
        var result = new List<StatusObservation>();
        foreach (var sample in snapshot.Take(128))
        {
            if (sample.Id == 0 || !float.IsFinite(sample.Remaining) || sample.Remaining < 0) continue;
            var key = (sample.Id, sample.Source);
            current[key] = sample;
            if (ready && (!previous.TryGetValue(key, out var old) || old.Parameter != sample.Parameter ||
                sample.Remaining > old.Remaining + 1))
                result.Add(new StatusObservation { Time = time, StatusId = sample.Id, Duration = sample.Remaining,
                    Parameter = sample.Parameter, SourceId = sample.Source });
        }
        previous = current;
        ready = true;
        return result;
    }
}

/// <summary>Caller supplies a frozen plan. This evaluator never reads game state or changes slides.</summary>
public sealed class AdaptiveEngine
{
    private sealed class Armed
    {
        public required AdaptiveMechanic Rule;
        public float Start;
        public int Occurrence;
        public float FirstMatch = -1;
        public readonly Dictionary<int, StatusObservation> Matches = new();
    }
    private readonly List<AdaptiveMechanic> rules;
    private readonly List<Armed> armed = new();
    public int ActiveRuleCount => rules.Count;
    public AdaptiveEngine(PlanDocument plan, uint territory)
    {
        var candidates = plan.AdaptiveMechanics.Take(128)
            .Where(r => r != null && r.Enabled && r.TerritoryId == territory && r.IsValid(plan)).ToList();
        // Alternatives belong to a single rule; overlapping rules cannot make independent
        // decisions at different times and silently replace one another's assignment.
        rules = candidates.Where(r => candidates.Count(other => r.Overlaps(other)) == 1).ToList();
    }

    public void Arm(uint action, int occurrence, float time)
    {
        foreach (var rule in rules.Where(r => r.AnchorActionId == action && (r.Occurrence == 0 || r.Occurrence == occurrence)))
        {
            armed.RemoveAll(a => a.Rule == rule);
            armed.Add(new Armed { Rule = rule, Start = time, Occurrence = occurrence });
        }
    }

    public List<AdaptiveDecision> Update(IReadOnlyList<StatusObservation> observations, float time)
    {
        var decisions = new List<AdaptiveDecision>();
        if (!float.IsFinite(time) || time < 0) return decisions;
        foreach (var state in armed.ToArray())
        {
            foreach (var observed in observations)
            {
                if (!float.IsFinite(observed.Time) || !float.IsFinite(observed.Duration) || observed.Duration < 0) continue;
                if (observed.Time < state.Start || observed.Time > state.Start + state.Rule.WindowSeconds) continue;
                for (var i = 0; i < state.Rule.Branches.Count; i++)
                {
                    var b = state.Rule.Branches[i];
                    if (observed.StatusId != b.StatusId || observed.Duration < b.MinimumSeconds || observed.Duration >= b.MaximumSeconds ||
                        (b.Parameter >= 0 && b.Parameter != observed.Parameter)) continue;
                    state.Matches.TryAdd(i, observed);
                    if (state.FirstMatch < 0) state.FirstMatch = time;
                }
            }
            var expired = time >= state.Start + state.Rule.WindowSeconds;
            if (!expired && (state.FirstMatch < 0 || time - state.FirstMatch < .3f)) continue;
            var decision = new AdaptiveDecision { Time = time, Mechanic = state.Rule.Label,
                AnchorActionId = state.Rule.AnchorActionId, Occurrence = state.Occurrence };
            if (state.Matches.Count == 1)
            {
                var match = state.Matches.First();
                var b = state.Rule.Branches[match.Key];
                var o = match.Value;
                decision.SlideId = b.SlideId;
                decision.Reason = $"{b.Label}: status #{o.StatusId}, initial observed duration {o.Duration:0.0}s, parameter {o.Parameter}, source #{o.SourceId}.";
            }
            else decision.Reason = state.Matches.Count == 0 ? "No matching status observed within the assignment window." :
                "Conflicting branches matched; no destination selected.";
            decisions.Add(decision);
            armed.Remove(state);
        }
        if (decisions.Where(d => d.SlideId.Length > 0).Select(d => d.SlideId).Distinct().Count() > 1)
            foreach (var d in decisions) { d.SlideId = ""; d.Reason += " Conflicting mechanics; navigation withheld."; }
        return decisions;
    }
}
