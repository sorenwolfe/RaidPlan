using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Shikari.Model;
using Shikari.Services.Replay;
using Shikari.UI.Theme;

namespace Shikari.UI;

public sealed partial class MainWindow
{
    private void DrawAdaptiveTab(PlanDocument plan)
    {
        using (Plugin.Fonts.PushHeading()) ImGui.TextUnformatted("ADAPTIVE MECHANICS");
        ImGui.TextWrapped("Choose a different slide when you receive a status assignment. Rules use your own statuses and are captured at pull start.");
        ImGui.TextColored(Palette.Vec(Palette.Attention), "Author and verify each rule before enabling it. Imported rules start disabled.");
        ImGui.TextWrapped(Plugin.Adaptive.Status);
        DrawStatusDiscovery();
        ImGui.Separator();
        ImGui.BeginDisabled(Plugin.Encounter.InCombat);
        if (Plugin.Encounter.InCombat) ImGui.TextDisabled("Rule editing resumes after this pull.");
        ImGui.BeginDisabled(plan.AdaptiveMechanics.Count >= 128);
        if (ImGui.Button("Add status mechanic"))
        {
            var rule = new AdaptiveMechanic { TerritoryId = Plugin.ClientState.TerritoryType };
            rule.Branches.Add(new StatusBranch { SlideId = CurrentSlide?.Id ?? "" });
            plan.AdaptiveMechanics.Add(rule); MarkDirty();
        }
        ImGui.EndDisabled();
        for (var i = 0; i < plan.AdaptiveMechanics.Count; i++)
        {
            var rule = plan.AdaptiveMechanics[i];
            ImGui.PushID(i);
            if (ImGui.CollapsingHeader($"{rule.Label}  /  {(rule.Enabled ? "ENABLED" : "DISABLED")}###adaptive-rule"))
            {
                var label = rule.Label;
                if (UiHelpers.InputTextHint("Name", "Mechanic name", ref label, 100)) { rule.Label = label; MarkDirty(); }
                var territory = (int)rule.TerritoryId;
                if (ImGui.InputInt("Territory ID", ref territory)) { rule.TerritoryId = (uint)Math.Max(0, territory); MarkDirty(); }
                ImGui.SameLine();
                if (ImGui.SmallButton("Use current territory")) { rule.TerritoryId = Plugin.ClientState.TerritoryType; MarkDirty(); }
                var action = (int)rule.AnchorActionId;
                if (ImGui.InputInt("Assignment cast ID", ref action)) { rule.AnchorActionId = (uint)Math.Max(0, action); MarkDirty(); }
                if (ImGui.BeginCombo("Use observed cast", "Choose from this pull"))
                {
                    foreach (var cast in Plugin.Encounter.RecentCasts.Reverse().Take(50))
                        if (ImGui.Selectable($"{cast.ActionName}  #{cast.ActionId}, use {cast.Occurrence}##{cast.CombatTime}"))
                        { rule.AnchorActionId = cast.ActionId; rule.Occurrence = cast.Occurrence; MarkDirty(); }
                    ImGui.EndCombo();
                }
                var occurrence = rule.Occurrence;
                if (ImGui.InputInt("Cast occurrence (0 = every use)", ref occurrence)) { rule.Occurrence = Math.Max(0, occurrence); MarkDirty(); }
                var window = rule.WindowSeconds;
                if (ImGui.SliderFloat("Assignment window after cast start", ref window, 1, 60, "%.1f s", ImGuiSliderFlags.None))
                { rule.WindowSeconds = window; MarkDirty(); }
                ImGui.TextDisabled("Only new statuses or refreshes inside this window can select a branch.");
                for (var b = 0; b < rule.Branches.Count; b++)
                {
                    ImGui.PushID(b);
                    var branch = rule.Branches[b];
                    ImGui.Separator();
                    var name = branch.Label;
                    if (UiHelpers.InputTextHint("Branch", "Short / long / assignment name", ref name, 100)) { branch.Label = name; MarkDirty(); }
                    var status = (int)branch.StatusId;
                    if (ImGui.InputInt("Status ID", ref status)) { branch.StatusId = (uint)Math.Max(0, status); MarkDirty(); }
                    if (ImGui.BeginCombo("Use observed status", "Choose a captured assignment"))
                    {
                        foreach (var observed in Plugin.Adaptive.Recent.Reverse().Take(80))
                            if (ImGui.Selectable($"#{observed.StatusId} / {observed.Duration:0.0}s / param {observed.Parameter} / {observed.Time:0.0}s##status"))
                            { branch.StatusId = observed.StatusId; branch.Parameter = observed.Parameter; MarkDirty(); }
                        ImGui.EndCombo();
                    }
                    var parameter = branch.Parameter;
                    if (ImGui.InputInt("Parameter (-1 = any)", ref parameter)) { branch.Parameter = Math.Clamp(parameter, -1, ushort.MaxValue); MarkDirty(); }
                    var min = branch.MinimumSeconds; var max = branch.MaximumSeconds;
                    if (ImGui.InputFloat("Initial duration at least", ref min)) { branch.MinimumSeconds = Math.Clamp(min, 0, 3599); MarkDirty(); }
                    if (ImGui.InputFloat("Initial duration below", ref max)) { branch.MaximumSeconds = Math.Clamp(max, 1, 3600); MarkDirty(); }
                    ImGui.TextDisabled("Use a range around expected durations to allow for observation delay.");
                    if (ImGui.BeginCombo("Show slide", plan.FindSlide(branch.SlideId)?.Title ?? "Choose a destination"))
                    {
                        foreach (var slide in plan.Slides)
                            if (ImGui.Selectable(slide.Title + "##" + slide.Id, slide.Id == branch.SlideId)) { branch.SlideId = slide.Id; MarkDirty(); }
                        ImGui.EndCombo();
                    }
                    if (ImGui.SmallButton("Remove branch")) { rule.Branches.RemoveAt(b--); MarkDirty(); }
                    ImGui.PopID();
                }
                ImGui.BeginDisabled(rule.Branches.Count >= 16);
                if (ImGui.Button("Add branch")) { rule.Branches.Add(new StatusBranch()); MarkDirty(); }
                ImGui.EndDisabled();
                var overlaps = plan.AdaptiveMechanics.Any(other => other != rule && other.Enabled && rule.Overlaps(other));
                var valid = rule.IsValid(plan) && !overlaps;
                if (!rule.IsValid(plan)) ImGui.TextColored(Palette.Vec(Palette.Attention), "Set a territory, cast, valid duration ranges and destination slides before enabling.");
                if (overlaps) ImGui.TextColored(Palette.Vec(Palette.Attention), "Another enabled rule covers this cast occurrence. Put alternatives in one mechanic.");
                ImGui.BeginDisabled(!valid && !rule.Enabled);
                var enabled = rule.Enabled;
                if (ImGui.Checkbox("Enable this verified rule", ref enabled)) { rule.Enabled = enabled; MarkDirty(); }
                ImGui.EndDisabled();
                if (ImGui.SmallButton("Delete mechanic")) { plan.AdaptiveMechanics.RemoveAt(i--); MarkDirty(); }
            }
            ImGui.PopID();
        }
        ImGui.EndDisabled();
    }

    private static void DrawStatusDiscovery()
    {
        if (!ImGui.CollapsingHeader("Observed assignments")) return;
        ImGui.TextWrapped("New local statuses and refreshes are captured during combat. Existing statuses at the first readable snapshot are a baseline. History resets on the next pull.");
        if (ImGui.BeginChild("##observed-statuses", new Vector2(0, 160 * UiHelpers.Scale), true, ImGuiWindowFlags.None))
            foreach (var status in Plugin.Adaptive.Recent.Reverse())
                ImGui.TextUnformatted($"{status.Time:0.0}s  /  status #{status.StatusId}  /  initial {status.Duration:0.0}s  /  param {status.Parameter}");
        ImGui.EndChild();
    }

    private void DrawAdaptiveEvidence(ReplayAttempt attempt)
    {
        if (!ImGui.CollapsingHeader($"Adaptive evidence  /  {attempt.AdaptiveDecisions.Count} decisions")) return;
        ImGui.TextDisabled("Select a decision to seek to its observation time. Unmatched mechanics have no inferred destination.");
        if (ImGui.BeginChild("##adaptive-evidence", new Vector2(0, 160 * UiHelpers.Scale), true, ImGuiWindowFlags.None))
        {
            for (var i = 0; i < attempt.AdaptiveDecisions.Count; i++)
            {
                var decision = attempt.AdaptiveDecisions[i];
                if (ImGui.Selectable($"{decision.Time:0.0}s / {decision.Mechanic} / {decision.Navigation}##decision-{i}"))
                { reviewTime = decision.Time; reviewPlaying = false; }
                ImGui.TextWrapped(decision.Reason);
            }
            if (ImGui.TreeNode($"Captured statuses ({attempt.StatusObservations.Count})"))
            {
                foreach (var status in attempt.StatusObservations)
                    ImGui.TextUnformatted($"{status.Time:0.0}s / #{status.StatusId} / initial {status.Duration:0.0}s / param {status.Parameter}");
                ImGui.TreePop();
            }
        }
        ImGui.EndChild();
    }
}
