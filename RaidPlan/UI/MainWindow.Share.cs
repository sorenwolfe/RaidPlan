using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RaidPlan.Model;
using RaidPlan.Services;

namespace RaidPlan.UI;

public sealed partial class MainWindow
{
    private void DrawShareTab(RaidPlanDocument plan)
    {
        ImGui.TextWrapped(
            "A plan travels as one line of text. Copy it, drop it in your static's Discord, and " +
            "everyone pastes it back here. Nothing leaves your machine and there is no server involved.");

        ImGui.Spacing();
        ImGui.TextDisabled("Export");
        ImGui.Separator();

        if (ImGui.Button("Copy share code to clipboard", Vector2.Zero))
        {
            try
            {
                var code = ShareCode.Encode(plan);
                ImGui.SetClipboardText(code);
                importStatus = $"Copied {code.Length:N0} characters. Paste it wherever your team talks.";
                importStatusIsError = false;
            }
            catch (Exception ex)
            {
                importStatus = "Could not build a share code: " + ex.Message;
                importStatusIsError = true;
                Plugin.Log.Error(ex, "Share code export failed.");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Save code to a file", Vector2.Zero))
            ExportCodeToFile(plan);

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Chat apps cut long messages off — Discord stops at 2,000 characters. A big plan is " +
            "easier to hand over as a text file, which also survives being forwarded intact.");

        ImGui.SameLine();
        ImGui.TextDisabled($"{plan.Slides.Count} slides · {plan.Timeline.Count} steps · {plan.Roster.Count} seats");

        var estimate = EstimateCodeLength(plan);
        if (estimate > 2000)
        {
            ImGui.TextColored(
                UiHelpers.Pack(new Vector4(1f, 0.78f, 0.35f, 1f)),
                $"This plan makes a ~{estimate:N0} character code, past Discord's 2,000 limit — send it as a file instead.");
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Import");
        ImGui.Separator();

        ImGui.TextUnformatted("Paste a code below:");
        UiHelpers.InputMultiline("##import-code", ref importBuffer, new Vector2(-1, 90 * UiHelpers.Scale), 1024 * 512);

        if (ImGui.Button("Paste from clipboard", Vector2.Zero))
        {
            try
            {
                importBuffer = ImGui.GetClipboardText() ?? string.Empty;
            }
            catch (Exception ex)
            {
                importStatus = "Could not read the clipboard: " + ex.Message;
                importStatusIsError = true;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Import as a new plan", Vector2.Zero))
            DoImport(replaceExisting: false);

        ImGui.SameLine();
        if (ImGui.Button("Import and overwrite", Vector2.Zero))
            DoImport(replaceExisting: true);

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "\"Overwrite\" replaces the stored copy of the plan that shares this code's id — that is " +
            "how you take an updated version from your raid lead without ending up with six copies.");

        if (!string.IsNullOrEmpty(importStatus))
        {
            ImGui.Spacing();
            if (importStatusIsError)
                ImGui.TextColored(UiHelpers.Pack(new Vector4(1f, 0.45f, 0.4f, 1f)), importStatus);
            else
                ImGui.TextColored(UiHelpers.Pack(new Vector4(0.5f, 0.9f, 0.5f, 1f)), importStatus);
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Plan details");
        ImGui.Separator();

        ImGui.SetNextItemWidth(300 * UiHelpers.Scale);
        var author = plan.Author;
        if (UiHelpers.InputTextHint("Author", "who wrote this", ref author, 64))
        {
            plan.Author = author;
            MarkDirty();
        }

        ImGui.TextUnformatted("Notes for the team");
        var notes = plan.Notes;
        if (UiHelpers.InputMultiline("##plan-notes", ref notes, new Vector2(-1, 140 * UiHelpers.Scale)))
        {
            plan.Notes = notes;
            MarkDirty();
        }
    }

    /// <summary>
    /// Encoding a heavy plan every frame just to show its length would be wasteful, so the
    /// figure is refreshed on a short timer and cached in between.
    /// </summary>
    private int EstimateCodeLength(RaidPlanDocument plan)
    {
        if (codeLengthPlanId == plan.Id && (DateTime.UtcNow - codeLengthCheckedUtc).TotalSeconds < 2)
            return cachedCodeLength;

        try
        {
            cachedCodeLength = ShareCode.Encode(plan).Length;
        }
        catch
        {
            cachedCodeLength = 0;
        }

        codeLengthPlanId = plan.Id;
        codeLengthCheckedUtc = DateTime.UtcNow;
        return cachedCodeLength;
    }

    private void ExportCodeToFile(RaidPlanDocument plan)
    {
        try
        {
            var folder = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "shared");
            Directory.CreateDirectory(folder);

            var safe = new string(plan.Name.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_').ToArray()).Trim();
            if (safe.Length == 0)
                safe = "plan";

            var path = Path.Combine(folder, safe + ".raidplan.txt");
            File.WriteAllText(path, ShareCode.Encode(plan), Encoding.UTF8);

            importStatus = "Saved to " + path;
            importStatusIsError = false;
        }
        catch (Exception ex)
        {
            importStatus = "Could not write the file: " + ex.Message;
            importStatusIsError = true;
            Plugin.Log.Error(ex, "Share code file export failed.");
        }
    }

    private void DoImport(bool replaceExisting)
    {
        if (!ShareCode.TryDecode(importBuffer, out var imported, out var error) || imported == null)
        {
            importStatus = error;
            importStatusIsError = true;
            return;
        }

        Plugin.Plans.SaveActive();
        Plugin.Plans.Import(imported, replaceExisting);
        slideIndex = 0;
        selectedEntryId = null;
        canvas.Select(null);
        importBuffer = string.Empty;
        importStatus = $"Imported \"{imported.Name}\" — {imported.Slides.Count} slides and {imported.Timeline.Count} steps.";
        importStatusIsError = false;
    }

    // ---------------------------------------------------------------- live tab

    private void DrawLiveTab(RaidPlanDocument plan)
    {
        var encounter = Plugin.Encounter;

        ImGui.TextDisabled("Pull status");
        ImGui.Separator();

        if (encounter.InCombat)
            ImGui.TextColored(UiHelpers.Pack(new Vector4(0.5f, 0.9f, 0.5f, 1f)), $"In combat — {CallTemplate.FormatTime(encounter.CombatElapsed)}");
        else
            ImGui.TextDisabled("Out of combat.");

        ImGui.SameLine();
        if (ImGui.Button("Reset counters", Vector2.Zero))
            encounter.ResetPull();

        ImGui.SameLine();
        var logging = Plugin.Config.LogDetectedCasts;
        if (ImGui.Checkbox("Log every cast", ref logging))
        {
            Plugin.Config.LogDetectedCasts = logging;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Writes every hostile cast to /xllog with its time and occurrence number. Do one clean " +
            "pull with this on and you have the raw material for the whole timeline.");

        ImGui.Spacing();
        ImGui.TextDisabled("Casts seen this pull");
        ImGui.Separator();

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("##casts", 5, flags, new Vector2(0, 260 * UiHelpers.Scale), 0f))
        {
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70 * UiHelpers.Scale, 0);
            ImGui.TableSetupColumn("Cast", ImGuiTableColumnFlags.WidthStretch, 0f, 0);
            ImGui.TableSetupColumn("Use", ImGuiTableColumnFlags.WidthFixed, 50 * UiHelpers.Scale, 0);
            ImGui.TableSetupColumn("Bar", ImGuiTableColumnFlags.WidthFixed, 60 * UiHelpers.Scale, 0);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 90 * UiHelpers.Scale, 0);
            ImGui.TableHeadersRow();

            foreach (var cast in encounter.RecentCasts.AsEnumerable().Reverse().ToList())
            {
                ImGui.TableNextRow();
                ImGui.PushID("lc" + cast.ActionId + "-" + cast.Occurrence + "-" + cast.CombatTime);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(CallTemplate.FormatTime(cast.CombatTime));

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(cast.ActionName);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Action #{cast.ActionId}\nCast by {cast.CasterName}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(cast.Occurrence.ToString());

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{cast.TotalCastTime:0.0}s");

                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Add step"))
                {
                    var entry = new TimelineEntry
                    {
                        Label = cast.ActionName,
                        Trigger = TriggerKind.BossCast,
                        CastActionId = cast.ActionId,
                        CastName = cast.ActionName,
                        Occurrence = cast.Occurrence,
                        SortTime = cast.CombatTime,
                        LeadSeconds = Math.Clamp(cast.TotalCastTime - 1f, 1f, 10f),
                    };
                    plan.Timeline.Add(entry);
                    selectedEntryId = entry.Id;
                    MarkDirty();
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Recent calls");
        ImGui.Separator();

        if (Plugin.Reminders.ActiveCalls.Count == 0)
        {
            ImGui.TextDisabled("Nothing on screen right now.");
        }
        else
        {
            foreach (var call in Plugin.Reminders.ActiveCalls)
                ImGui.TextUnformatted($"{call.Headline}   {call.SubLine}");
        }
    }
}
