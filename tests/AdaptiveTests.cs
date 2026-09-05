using System;
using Shikari.Model;
using Shikari.Services.Adaptive;
namespace Shikari.Tests;
public static class AdaptiveTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        var tracker = new StatusTracker();
        Check(tracker.Observe(new[] { new StatusSample(10, 30, 0, 99) }, 0).Count == 0, "First snapshot establishes baseline");
        Check(tracker.Observe(Array.Empty<StatusSample>(), 1).Count == 0, "Loss is not a gain");
        var gained = tracker.Observe(new[] { new StatusSample(10, 30, 0, 99) }, 2);
        Check(gained.Count == 1 && gained[0].Duration == 30, "Capture initial observed duration");
        Check(tracker.Observe(new[] { new StatusSample(10, 10, 0, 99) }, 22).Count == 0, "Countdown cannot reclassify long as short");
        Check(tracker.Observe(new[] { new StatusSample(10, 30, 0, 99) }, 23).Count == 1, "Refresh creates a new observation");
        tracker.Invalidate();
        Check(tracker.Observe(new[] { new StatusSample(10, 30, 0, 99) }, 24).Count == 0, "Missing actor requires a fresh baseline");
        var plan = PlanDocument.CreateDefault();
        var alternate = new Slide { Title = "Long" }; plan.Slides.Add(alternate);
        var rule = new AdaptiveMechanic { Enabled = true, TerritoryId = 1, AnchorActionId = 100, Occurrence = 2 };
        rule.Branches.Add(new StatusBranch { StatusId = 10, MinimumSeconds = 0, MaximumSeconds = 20, SlideId = plan.Slides[0].Id });
        rule.Branches.Add(new StatusBranch { StatusId = 10, MinimumSeconds = 20, MaximumSeconds = 60, SlideId = alternate.Id });
        plan.AdaptiveMechanics.Add(rule);
        var engine = new AdaptiveEngine(plan, 1);
        engine.Arm(100, 1, 0);
        Check(engine.Update(gained, 2).Count == 0, "Wrong occurrence cannot arm");
        engine.Arm(100, 2, 1);
        Check(engine.Update(gained, 2).Count == 0, "Wait briefly for competing observations");
        var decisions = engine.Update(Array.Empty<StatusObservation>(), 2.4f);
        Check(decisions.Count == 1 && decisions[0].SlideId == alternate.Id, "Long branch selected");
        Check(engine.Update(gained, 3).Count == 0, "Decision emitted once per arm");
        engine.Arm(100, 2, 4);
        Check(engine.Update(gained, 4.5f).Count == 0, "Pre-anchor observations ignored");
        decisions = engine.Update(Array.Empty<StatusObservation>(), 20);
        Check(decisions.Count == 1 && decisions[0].SlideId == "", "Timeout has no destination");
        rule.Branches.Add(new StatusBranch { StatusId = 10, MaximumSeconds = 60, SlideId = plan.Slides[0].Id });
        engine = new AdaptiveEngine(plan, 1); engine.Arm(100, 2, 1);
        engine.Update(gained, 2);
        decisions = engine.Update(Array.Empty<StatusObservation>(), 2.4f);
        Check(decisions.Count == 1 && decisions[0].SlideId == "" && decisions[0].Reason.Contains("Conflicting"), "Conflicts do not guess");
        engine = new AdaptiveEngine(plan, 2); engine.Arm(100, 2, 1);
        Check(engine.Update(gained, 2).Count == 0 && engine.Update(gained, 20).Count == 0, "Wrong territory cannot arm");
        rule.Branches.RemoveAt(2);
        rule.Branches[1].Parameter = 5;
        engine = new AdaptiveEngine(plan, 1); engine.Arm(100, 2, 1); engine.Update(gained, 2);
        Check(engine.Update(Array.Empty<StatusObservation>(), 20)[0].SlideId == "", "Parameter mismatch cannot select a branch");
        rule.Branches[1].Parameter = -1;
        rule.Enabled = false;
        engine = new AdaptiveEngine(plan, 1); engine.Arm(100, 2, 1);
        Check(engine.Update(gained, 2).Count == 0 && engine.Update(gained, 20).Count == 0, "Disabled rule cannot arm");
        rule.Enabled = true;
        var duplicate = new AdaptiveMechanic { Enabled = true, TerritoryId = 1, AnchorActionId = 100, Occurrence = 0 };
        duplicate.Branches.Add(new StatusBranch { StatusId = 10, SlideId = alternate.Id });
        plan.AdaptiveMechanics.Add(duplicate);
        engine = new AdaptiveEngine(plan, 1);
        Check(engine.ActiveRuleCount == 0, "Overlapping wildcard/specific rules cannot independently overwrite assignments");
        Console.WriteLine("PASS: status baseline, initial duration, countdown, refresh, gaps, occurrence, scope, settling, conflicts, timeout, one decision per arm");
    }
}
