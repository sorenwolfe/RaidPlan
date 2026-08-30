using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using RaidPlan.Model;
using RaidPlan.Services;
using RaidPlan.Services.FfLogs;

namespace RaidPlan.UI;

public sealed partial class MainWindow
{
    private string reportInput = string.Empty;
    private string importStatusLine = string.Empty;
    private bool importFailed;
    private string importDetail = string.Empty;

    private List<LogFight>? fights;
    private int selectedFight = -1;
    private LogFightData? loadedFight;
    private bool importBusy;

    private readonly ImportOptions importOptions = new();
    private bool showCredentials;

    private void DrawImportTab(RaidPlanDocument plan)
    {
        ImGui.TextWrapped(
            "Paste an FF Logs link and pull the fight's timeline straight in, along with the " +
            "cooldowns each player pressed. Seats are matched to log players by job.");

        ImGui.Spacing();

        if (!HasCredentials())
        {
            ImGui.Separator();
            FfLogsCredentialsPanel.Draw(showInstructions: true);
            return;
        }

        // Set up and working: one line, with a way back to the boxes.
        FfLogsCredentialsPanel.DrawSummary();
        ImGui.SameLine();
        if (ImGui.SmallButton(showCredentials ? "Hide" : "Change"))
            showCredentials = !showCredentials;

        if (showCredentials)
        {
            ImGui.Spacing();
            FfLogsCredentialsPanel.Draw(showInstructions: false);
            ImGui.Separator();
        }

        ImGui.Spacing();

        if (string.IsNullOrEmpty(reportInput) && !string.IsNullOrEmpty(Plugin.Config.LastReportUrl))
            reportInput = Plugin.Config.LastReportUrl;

        ImGui.SetNextItemWidth(-120 * UiHelpers.Scale);
        UiHelpers.InputTextHint("##report", "https://www.fflogs.com/reports/…", ref reportInput, 256);

        ImGui.SameLine();
        ImGui.BeginDisabled(importBusy);
        if (ImGui.Button("Load fights", new Vector2(-1, 0)))
            LoadFights();
        ImGui.EndDisabled();

        var parsed = ReportUrl.Parse(reportInput);
        if (!parsed.IsValid && reportInput.Length > 0)
            ImGui.TextDisabled("No report code in that. A code is 16 letters and digits.");

        if (importBusy)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Talking to FF Logs…");
        }

        DrawImportStatus();

        if (fights is { Count: > 0 })
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Fight");
            ImGui.Separator();

            ImGui.SetNextItemWidth(-1);
            var preview = selectedFight >= 0 && selectedFight < fights.Count
                ? fights[selectedFight].Describe()
                : "Pick a pull";

            if (ImGui.BeginCombo("##fight", preview, ImGuiComboFlags.HeightLarge))
            {
                for (var i = 0; i < fights.Count; i++)
                {
                    if (ImGui.Selectable(fights[i].Describe() + "##f" + i, i == selectedFight, ImGuiSelectableFlags.None, Vector2.Zero))
                    {
                        selectedFight = i;
                        loadedFight = null;
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.BeginDisabled(importBusy || selectedFight < 0);
            if (ImGui.Button("Fetch this fight", Vector2.Zero))
                LoadFightData();
            ImGui.EndDisabled();
        }

        if (loadedFight != null)
            DrawPreviewAndApply(plan, loadedFight);
    }

    /// <summary>
    /// Credentials good enough to try an import with. Unchecked counts: someone who set them up
    /// before this check existed should not be stopped from importing.
    /// </summary>
    private static bool HasCredentials() => Plugin.FfLogsAuth.Usable;

    private void DrawImportStatus()
    {
        if (string.IsNullOrEmpty(importStatusLine))
            return;

        ImGui.Spacing();
        ImGui.TextColored(
            UiHelpers.Pack(importFailed
                ? new Vector4(1f, 0.45f, 0.4f, 1f)
                : new Vector4(0.5f, 0.9f, 0.5f, 1f)),
            importStatusLine);

        if (string.IsNullOrEmpty(importDetail))
            return;

        if (!ImGui.TreeNode("What FF Logs sent back###import-detail"))
            return;

        ImGui.TextWrapped(importDetail.Length > 2000 ? importDetail[..2000] + "…" : importDetail);
        if (ImGui.SmallButton("Copy"))
            ImGui.SetClipboardText(importDetail);
        ImGui.TreePop();
    }

    private void DrawPreviewAndApply(RaidPlanDocument plan, LogFightData data)
    {
        ImGui.Spacing();
        ImGui.TextDisabled("What's in this pull");
        ImGui.Separator();

        var players = data.Actors.Where(a => a.IsPlayer).ToList();
        ImGui.TextUnformatted(
            $"{data.EnemyCasts.Count} boss casts, {data.PlayerCasts.Count} player casts, {players.Count} players.");

        var seats = BuildSeatJobs(plan);
        var matches = LogImporter.MatchSeats(seats, data.Actors);

        ImGui.Spacing();
        if (matches.Count == 0)
        {
            ImGui.TextColored(
                UiHelpers.Pack(new Vector4(0.9f, 0.85f, 0.45f, 1f)),
                "No seats matched. Set each seat's job on the Roster tab and the log's players will line up.");
        }
        else
        {
            ImGui.TextDisabled($"{matches.Count} seat(s) matched by job:");
            foreach (var match in matches)
            {
                var seat = plan.Roster[match.SeatIndex];
                ImGui.TextUnformatted($"    {seat.DisplayName}  ←  {match.PlayerName} ({match.JobName})");
            }
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Bring in");
        ImGui.Separator();

        var timeline = importOptions.ImportTimeline;
        if (ImGui.Checkbox("Timeline steps", ref timeline))
            importOptions.ImportTimeline = timeline;

        ImGui.SameLine();
        var assignments = importOptions.ImportAssignments;
        if (ImGui.Checkbox("Cooldown assignments", ref assignments))
            importOptions.ImportAssignments = assignments;

        ImGui.SameLine();
        var slides = importOptions.CreateSlides;
        if (ImGui.Checkbox("A slide per step", ref slides))
            importOptions.CreateSlides = slides;

        var onlyBar = importOptions.OnlyCastsWithBar;
        if (ImGui.Checkbox("Only casts with a bar", ref onlyBar))
            importOptions.OnlyCastsWithBar = onlyBar;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Drops instants, which are mostly auto-attacks and filler.");

        ImGui.SameLine();
        var window = importOptions.WindowBefore;
        ImGui.SetNextItemWidth(180 * UiHelpers.Scale);
        if (ImGui.SliderFloat("Look back from a cast", ref window, 5f, 40f, "%.0f s", ImGuiSliderFlags.None))
            importOptions.WindowBefore = window;

        if (ImGui.IsItemHovered())
        {
            UiHelpers.Tooltip(
                "A cooldown pressed this long before a boss cast is taken as being for it. " +
                "Longer catches pre-planned mitigation; shorter avoids grabbing the wrong mechanic.");
        }

        var preview = LogImporter.BuildTimeline(data, importOptions);
        ImGui.Spacing();
        ImGui.TextDisabled($"That gives {preview.Count} step(s).");

        ImGui.Spacing();
        if (ImGui.Button("Import into this plan", Vector2.Zero))
        {
            var result = LogImporter.Apply(
                plan, data, importOptions, seats,
                id => Plugin.Actions.Get(id)?.IsCooldown ?? false);

            MarkDirty();
            importFailed = false;
            importDetail = string.Empty;
            importStatusLine = result.Summary();

            if (result.CooldownsUnattributed > 0)
                importStatusLine += $" {result.CooldownsUnattributed} cooldown(s) didn't line up with a step.";
            if (result.Unmatched.Count > 0)
                importStatusLine += " Unmatched: " + string.Join(", ", result.Unmatched.Take(4)) + ".";
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Steps already on your timeline are left alone, so importing a second pull only fills " +
            "in what's missing. Assignments are added, never removed.");
    }

    private static List<SeatJob> BuildSeatJobs(RaidPlanDocument plan)
    {
        var seats = new List<SeatJob>();
        for (var i = 0; i < plan.Roster.Count; i++)
        {
            var job = Plugin.Actions.Job(plan.Roster[i].JobId);
            if (job != null)
                seats.Add(new SeatJob(i, job.Name, job.Abbreviation));
        }

        return seats;
    }

    private void LoadFights()
    {
        var parsed = ReportUrl.Parse(reportInput);
        if (!parsed.IsValid)
        {
            Fail("No report code in that link.");
            return;
        }

        Plugin.Config.LastReportUrl = reportInput.Trim();
        Plugin.SaveConfig();

        Run(async cancel =>
        {
            var list = await Plugin.FfLogs.GetFightsAsync(
                Plugin.Config.FfLogsClientId, Plugin.Config.FfLogsClientSecret, parsed.Code, cancel);

            fights = list;
            loadedFight = null;

            selectedFight = parsed.FightId switch
            {
                ReportUrl.LastFight => list.Count - 1,
                { } id => list.FindIndex(f => f.Id == id),
                _ => -1,
            };

            if (selectedFight < 0 && list.Count > 0)
                selectedFight = list.FindIndex(f => f.Kill) is var kill && kill >= 0 ? kill : list.Count - 1;

            importStatusLine = $"Found {list.Count} fight(s).";
            importFailed = false;
        });
    }

    private void LoadFightData()
    {
        var parsed = ReportUrl.Parse(reportInput);
        if (!parsed.IsValid || fights == null || selectedFight < 0)
            return;

        var fight = fights[selectedFight];

        Run(async cancel =>
        {
            loadedFight = await Plugin.FfLogs.GetFightDataAsync(
                Plugin.Config.FfLogsClientId, Plugin.Config.FfLogsClientSecret, parsed.Code, fight, cancel);

            importStatusLine = $"Loaded {fight.Name}.";
            importFailed = false;
        });
    }

    private void Run(Func<CancellationToken, Task> work)
    {
        importBusy = true;
        importStatusLine = string.Empty;
        importDetail = string.Empty;

        var cancel = Plugin.Shutdown;

        Task.Run(async () =>
        {
            try
            {
                await work(cancel);
            }
            catch (OperationCanceledException)
            {
                // Plugin is unloading mid-request. Nothing left to report to.
                return;
            }
            catch (FfLogsException ex)
            {
                if (cancel.IsCancellationRequested)
                    return;

                Fail(ex.Message, ex.Detail);
            }
            catch (Exception ex)
            {
                if (cancel.IsCancellationRequested)
                    return;

                Fail("Import failed: " + ex.Message);
                Plugin.Log.Error(ex, "FF Logs import failed.");
            }
            finally
            {
                importBusy = false;
            }
        }, cancel);
    }

    private void Fail(string message, string? detail = null)
    {
        importStatusLine = message;
        importFailed = true;
        importDetail = detail ?? string.Empty;
    }
}
