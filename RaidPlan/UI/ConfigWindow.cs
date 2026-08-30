using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using RaidPlan.Model;
using RaidPlan.Services;
using RaidPlan.UI.Theme;

namespace RaidPlan.UI;

/// <summary>Team profiles and delivery settings.</summary>
public sealed class ConfigWindow : Window, IDisposable
{
    /// <summary>Set from anywhere to have the window open on the next frame.</summary>
    public static bool RequestOpen;

    public ConfigWindow()
        : base("RaidPlan settings###raidplan-config", ImGuiWindowFlags.None)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 460),
            MaximumSize = new Vector2(1600, 1400),
        };

        Size = new Vector2(600, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    private ThemeScope theme;

    public override void PreDraw() => theme = ThemeScope.Push();

    public override void PostDraw() => theme.Dispose();

    public override void PreOpenCheck()
    {
        if (!RequestOpen)
            return;

        RequestOpen = false;
        IsOpen = true;
    }

    public override void Draw()
    {
        // Sits on the background list so it lands under the window's own fill.
        if (Plugin.Config.ThemeEnabled && Plugin.Config.ThemeShadows)
        {
            var pos = ImGui.GetWindowPos();
            Sprites.Shadow(ImGui.GetBackgroundDrawList(), pos, pos + ImGui.GetWindowSize(),
                18f * UiHelpers.Scale, 0.45f);
        }

        var config = Plugin.Config;
        var team = config.GetActiveTeam();

        ImGui.TextDisabled("Team profile");
        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "One profile per group you raid with. The plan itself is shared; how loudly and in what " +
            "words it talks to you is yours.");

        ImGui.Separator();

        ImGui.SetNextItemWidth(220 * UiHelpers.Scale);
        if (ImGui.BeginCombo("##team-select", team.Name, ImGuiComboFlags.None))
        {
            foreach (var profile in config.Teams)
            {
                if (ImGui.Selectable(profile.Name + "##" + profile.Id, profile.Id == team.Id, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    config.ActiveTeamId = profile.Id;
                    Plugin.SaveConfig();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("New", Vector2.Zero))
        {
            var profile = new TeamProfile { Name = "Team " + (config.Teams.Count + 1) };
            config.Teams.Add(profile);
            config.ActiveTeamId = profile.Id;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        if (ImGui.Button("Duplicate", Vector2.Zero))
        {
            var copy = team.Clone();
            config.Teams.Add(copy);
            config.ActiveTeamId = copy.Id;
            Plugin.SaveConfig();
        }

        if (config.Teams.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.Button("Delete", Vector2.Zero))
            {
                config.Teams.Remove(team);
                config.ActiveTeamId = config.Teams[0].Id;
                Plugin.SaveConfig();
                return;
            }
        }

        ImGui.SetNextItemWidth(300 * UiHelpers.Scale);
        var name = team.Name;
        if (UiHelpers.InputText("Profile name", ref name, 64))
        {
            team.Name = name;
            Plugin.SaveConfig();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("How calls reach you");
        ImGui.Separator();

        DrawChannelToggle(team, ReminderChannel.Overlay, "On-screen banner");
        DrawChannelToggle(team, ReminderChannel.Chat, "Chat log");
        DrawChannelToggle(team, ReminderChannel.Toast, "Dalamud notification");
        DrawChannelToggle(team, ReminderChannel.Sound, "Sound effect");

        if ((team.Channels & ReminderChannel.Sound) != 0)
        {
            ImGui.SetNextItemWidth(150 * UiHelpers.Scale);
            var sound = (int)team.SoundEffectId;
            if (ImGui.SliderInt("Sound effect", ref sound, 1, 16, "<se.%d>", ImGuiSliderFlags.None))
            {
                team.SoundEffectId = (uint)sound;
                Plugin.SaveConfig();
            }
        }

        if ((team.Channels & ReminderChannel.Chat) != 0)
        {
            ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
            var prefix = team.ChatPrefix;
            if (UiHelpers.InputText("Chat tag", ref prefix, 32))
            {
                team.ChatPrefix = prefix;
                Plugin.SaveConfig();
            }
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Banner");
        ImGui.Separator();

        var hold = team.OverlayHoldSeconds;
        ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
        if (ImGui.SliderFloat("Stays up for", ref hold, 1f, 15f, "%.1f s", ImGuiSliderFlags.None))
        {
            team.OverlayHoldSeconds = hold;
            Plugin.SaveConfig();
        }

        var scale = team.OverlayTextScale;
        ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
        if (ImGui.SliderFloat("Text size", ref scale, 1f, 4f, "%.1f×", ImGuiSliderFlags.None))
        {
            team.OverlayTextScale = scale;
            Plugin.SaveConfig();
        }

        var textColour = team.OverlayTextColor;
        if (ImGui.ColorEdit4("Text colour", ref textColour, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            team.OverlayTextColor = textColour;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        var backColour = team.OverlayBackgroundColor;
        if (ImGui.ColorEdit4("Backing", ref backColour, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            team.OverlayBackgroundColor = backColour;
            Plugin.SaveConfig();
        }

        var unlocked = config.OverlayUnlocked;
        if (ImGui.Checkbox("Unlock the banner so I can drag it", ref unlocked))
        {
            config.OverlayUnlocked = unlocked;
            Plugin.SaveConfig();
        }

        if (config.OverlayUnlocked)
        {
            ImGui.SameLine();
            if (ImGui.Button("Reset position", Vector2.Zero))
            {
                config.OverlayAnchor = new Vector2(0.5f, 0.28f);
                Plugin.SaveConfig();
            }
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Wording");
        ImGui.Separator();

        ImGui.SetNextItemWidth(UiHelpers.WidthLeaving("(?)"));
        var template = team.DefaultTemplate;
        if (UiHelpers.InputTextHint("##default-template", "Fallback line for steps with no wording of their own", ref template, 256))
        {
            team.DefaultTemplate = template;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(string.Join("\n",
            new[] { "Placeholders:" }.Concat(CallTemplate.Tokens.Select(t => $"{t.Token}  —  {t.Description}"))));

        ImGui.Spacing();
        ImGui.TextDisabled("Behaviour");
        ImGui.Separator();

        var enabled = config.RemindersEnabled;
        if (ImGui.Checkbox("Shotcalls on", ref enabled))
        {
            config.RemindersEnabled = enabled;
            Plugin.SaveConfig();
        }

        var onlyDuty = team.OnlyInDuty;
        if (ImGui.Checkbox("Only inside duties", ref onlyDuty))
        {
            team.OnlyInDuty = onlyDuty;
            Plugin.SaveConfig();
        }

        var others = team.ShowOtherPlayersCalls;
        if (ImGui.Checkbox("Also show calls aimed at other seats", ref others))
        {
            team.ShowOtherPlayersCalls = others;
            Plugin.SaveConfig();
        }


        var openOnDuty = config.OpenOnDutyStart;
        if (ImGui.Checkbox("Open the planner when a duty starts", ref openOnDuty))
        {
            config.OpenOnDutyStart = openOnDuty;
            Plugin.SaveConfig();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Following the fight");
        ImGui.Separator();

        var advance = config.AutoAdvanceSlides;
        if (ImGui.Checkbox("Move the slide on as the fight goes", ref advance))
        {
            config.AutoAdvanceSlides = advance;
            Plugin.Director.ClearSuppression();
            Plugin.SaveConfig();
        }

        if (config.AutoAdvanceSlides)
        {
            var onCast = config.AutoAdvanceOnCast;
            if (ImGui.Checkbox("Switch the moment the cast is seen", ref onCast))
            {
                config.AutoAdvanceOnCast = onCast;
                Plugin.SaveConfig();
            }

            ImGui.SameLine();
            UiHelpers.HelpMarker(
                "On: the slide changes as soon as the boss starts a cast one of your steps is " +
                "anchored to.\n\nOff: the slide arrives with the shotcall instead, which is a few " +
                "seconds earlier for learned steps and a few seconds later for cast-anchored ones.");

            var resetOnWipe = config.ResetSlidesOnWipe;
            if (ImGui.Checkbox("Back to the first slide on a wipe", ref resetOnWipe))
            {
                config.ResetSlidesOnWipe = resetOnWipe;
                Plugin.SaveConfig();
            }

            var overrideHold = config.ManualOverrideSeconds;
            ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
            if (ImGui.SliderFloat("Pause after I change slides", ref overrideHold, 0f, 60f, "%.0f s", ImGuiSliderFlags.None))
            {
                config.ManualOverrideSeconds = overrideHold;
                Plugin.SaveConfig();
            }

            ImGui.SameLine();
            UiHelpers.HelpMarker(
                "Flicking back to check something during a pull shouldn't have the plugin drag you " +
                "away again. A wipe cancels the pause regardless.");
        }

        ImGui.Spacing();
        ImGui.TextDisabled("FF Logs");
        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Only needed for importing a fight's timeline from a log. Everything else works " +
            "without it.");

        ImGui.Separator();
        FfLogsCredentialsPanel.Draw(showInstructions: true);

        ImGui.Spacing();
        ImGui.TextDisabled("Look");
        ImGui.Separator();

        var themed = config.ThemeEnabled;
        if (ImGui.Checkbox("RaidPlan's own dark theme", ref themed))
        {
            config.ThemeEnabled = themed;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Off puts the windows back to whatever style Dalamud is using, which is the right " +
            "choice if you already theme your plugins to match.");

        if (config.ThemeEnabled)
        {
            var accent = Palette.Vec(Palette.Accent);
            if (ImGui.ColorEdit4("Accent", ref accent, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha))
            {
                config.ThemeAccent =
                    ((uint)(Math.Clamp(accent.X, 0f, 1f) * 255f) << 16) |
                    ((uint)(Math.Clamp(accent.Y, 0f, 1f) * 255f) << 8) |
                    (uint)(Math.Clamp(accent.Z, 0f, 1f) * 255f);
                Plugin.SaveConfig();
            }

            ImGui.SameLine();
            foreach (var (presetName, value) in ThemePresets)
            {
                if (ImGui.Button(presetName, Vector2.Zero))
                {
                    config.ThemeAccent = value;
                    Plugin.SaveConfig();
                }

                ImGui.SameLine();
            }

            if (ImGui.Button("Reset", Vector2.Zero))
            {
                config.ThemeAccent = 0;
                Plugin.SaveConfig();
            }

            var shadows = config.ThemeShadows;
            if (ImGui.Checkbox("Drop shadow behind the windows", ref shadows))
            {
                config.ThemeShadows = shadows;
                Plugin.SaveConfig();
            }

            var toolIcons = config.ThemeToolIcons;
            if (ImGui.Checkbox("Icons on the drawing tools", ref toolIcons))
            {
                config.ThemeToolIcons = toolIcons;
                Plugin.SaveConfig();
            }

            ImGui.SameLine();
            UiHelpers.HelpMarker("Off goes back to the written tool names. The tooltips are the same either way.");
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Mini plan");
        ImGui.Separator();

        ImGui.TextWrapped(
            "A small copy of the current slide, about the size of the minimap, for reading during " +
            "a pull. It ignores the mouse while you're in combat, so it can't swallow a click.");
        ImGui.Spacing();

        var mode = (int)config.MiniPlanMode;
        ImGui.SetNextItemWidth(260 * UiHelpers.Scale);
        if (ImGui.Combo("Show it in", ref mode,
                "Never — only /raidplan mini\0Raids, savage and ultimate\0Any duty\0Only once the pull starts\0"))
        {
            config.MiniPlanMode = (MiniPlanVisibility)mode;
            Plugin.SaveConfig();
        }

        var size = config.MiniPlanSize;
        ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
        if (ImGui.SliderFloat("Size", ref size, 120f, 420f, "%.0f px", ImGuiSliderFlags.None))
        {
            config.MiniPlanSize = size;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker("The game's minimap is about 220 across, for comparison.");

        var opacity = config.MiniPlanOpacity;
        ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
        if (ImGui.SliderFloat("Background", ref opacity, 0.1f, 1f, "%.2f", ImGuiSliderFlags.None))
        {
            config.MiniPlanOpacity = opacity;
            Plugin.SaveConfig();
        }

        var highlight = config.MiniPlanHighlightMe;
        if (ImGui.Checkbox("Ring my own marker", ref highlight))
        {
            config.MiniPlanHighlightMe = highlight;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Finds your seat on the board by your character name, or by your job when it only " +
            "appears once in the roster. Pin a seat on the Roster tab if it picks wrong.");

        var miniUnlocked = config.MiniPlanUnlocked;
        if (ImGui.Checkbox("Keep it draggable during a pull", ref miniUnlocked))
        {
            config.MiniPlanUnlocked = miniUnlocked;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Off by default. Leaving it on means a click landing on the window during a mechanic " +
            "hits the overlay instead of the game.");

        ImGui.Spacing();
        ImGui.TextDisabled("Learning");
        ImGui.Separator();

        var learning = config.LearningEnabled;
        if (ImGui.Checkbox("Learn fight timings from my pulls", ref learning))
        {
            config.LearningEnabled = learning;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Records when each boss cast happens, per zone, and uses the median across pulls to " +
            "predict mechanics. Everything stays in the plugin's own folder on this machine.");

        if (config.LearningEnabled)
        {
            var confidence = config.MinimumPredictionConfidence;
            ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
            if (ImGui.SliderFloat("Trust predictions from", ref confidence, 0f, 1f, "%.2f", ImGuiSliderFlags.None))
            {
                config.MinimumPredictionConfidence = confidence;
                Plugin.SaveConfig();
            }

            ImGui.SameLine();
            UiHelpers.HelpMarker(
                "How sure RaidPlan has to be before a learned step fires ahead of the cast. Lower " +
                "gets you earlier warnings sooner, at the cost of the occasional call that lands " +
                "before the mechanic really arrives. Below the threshold a learned step simply " +
                "waits for the cast, exactly like a Boss cast step.");
        }

        var adjust = team.LeadTimeAdjust;
        ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
        if (ImGui.SliderFloat("Extra lead time", ref adjust, -3f, 5f, "%.1f s", ImGuiSliderFlags.None))
        {
            team.LeadTimeAdjust = adjust;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker("Added to every step's own lead time, for people who want a little more warning.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(Plugin.Actions.Ready
            ? "Action list loaded."
            : "Still reading the game's action list…");
    }

    private static readonly (string Name, uint Value)[] ThemePresets =
    {
        ("Indigo", Palette.DefaultAccent),
        ("Teal", Palette.Good),
        ("Amber", Palette.Attention),
    };

    private static void DrawChannelToggle(TeamProfile team, ReminderChannel channel, string label)
    {
        var on = (team.Channels & channel) != 0;
        if (!ImGui.Checkbox(label, ref on))
            return;

        if (on)
            team.Channels |= channel;
        else
            team.Channels &= ~channel;

        Plugin.SaveConfig();
    }

    public void Dispose()
    {
    }
}
