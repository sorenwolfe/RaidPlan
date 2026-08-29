using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RaidPlan.Services;
using RaidPlan.UI;

namespace RaidPlan;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/raidplan";
    private const string CommandAlias = "/rp";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static INotificationManager Notifications { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal static Configuration Config { get; private set; } = null!;
    internal static ActionIndex Actions { get; private set; } = null!;
    internal static PlanStore Plans { get; private set; } = null!;
    internal static EncounterMonitor Encounter { get; private set; } = null!;
    internal static ReminderEngine Reminders { get; private set; } = null!;
    internal static RosterResolver Roster { get; private set; } = null!;
    internal static EncounterLearner Learner { get; private set; } = null!;
    internal static SlideDirector Director { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("RaidPlan");

    private MainWindow mainWindow = null!;
    private ConfigWindow configWindow = null!;
    private OverlayWindow overlayWindow = null!;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Config.Teams.Count == 0)
            Config.Teams.Add(new TeamProfile());
        if (string.IsNullOrEmpty(Config.ActiveTeamId))
            Config.ActiveTeamId = Config.Teams[0].Id;

        Actions = new ActionIndex();
        Actions.BuildAsync();

        Plans = new PlanStore();
        Roster = new RosterResolver();

        // Order matters here: the monitor produces the events, the learner and the reminder
        // engine consume them, and the director consumes both.
        Encounter = new EncounterMonitor();
        Learner = new EncounterLearner();
        Reminders = new ReminderEngine();
        Director = new SlideDirector();

        mainWindow = new MainWindow();
        configWindow = new ConfigWindow();
        overlayWindow = new OverlayWindow();

        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);
        WindowSystem.AddWindow(overlayWindow);

        overlayWindow.IsOpen = true;

        Director.SlideRequested += mainWindow.OnDirectedSlide;
        Director.ResetRequested += mainWindow.OnDirectedReset;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage =
                "Open the raid strategy planner.\n" +
                "        /raidplan config  →  open settings\n" +
                "        /raidplan calls   →  toggle live shotcalls\n" +
                "        /raidplan next    →  show the next slide\n" +
                "        /raidplan prev    →  show the previous slide\n" +
                "        /raidplan follow  →  toggle slides following the fight\n" +
                "        /raidplan reset   →  jump back to the first slide",
        });

        CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "Shorthand for /raidplan.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMain;
        DutyState.DutyStarted += OnDutyStarted;
        DutyState.DutyCompleted += OnDutyCompleted;

        Log.Information("RaidPlan loaded.");
    }

    private void OnDutyStarted(Dalamud.Game.DutyState.IDutyStateEventArgs args)
    {
        if (Config.OpenOnDutyStart)
            mainWindow.IsOpen = true;
    }

    private void OnDutyCompleted(Dalamud.Game.DutyState.IDutyStateEventArgs args)
    {
        // Lets the learner mark the pull it just recorded as a clear.
        Learner.NoteClear();
    }

    private void OnCommand(string command, string args)
    {
        var argument = args.Trim().ToLowerInvariant();

        switch (argument)
        {
            case "":
                mainWindow.Toggle();
                break;

            case "config":
            case "settings":
                configWindow.Toggle();
                break;

            case "calls":
            case "reminders":
                Config.RemindersEnabled = !Config.RemindersEnabled;
                PluginInterface.SavePluginConfig(Config);
                ChatGui.Print(
                    Config.RemindersEnabled ? "Shotcalls are on." : "Shotcalls are off.",
                    "RaidPlan",
                    null);
                break;

            case "next":
                mainWindow.StepSlide(1);
                mainWindow.IsOpen = true;
                break;

            case "follow":
            case "auto":
                Config.AutoAdvanceSlides = !Config.AutoAdvanceSlides;
                Director.ClearSuppression();
                PluginInterface.SavePluginConfig(Config);
                ChatGui.Print(
                    Config.AutoAdvanceSlides
                        ? "Slides will follow the fight."
                        : "Slides will stay where you put them.",
                    "RaidPlan",
                    null);
                break;

            case "reset":
                mainWindow.ResetToFirstSlide();
                Director.ClearSuppression();
                break;

            case "prev":
            case "previous":
                mainWindow.StepSlide(-1);
                mainWindow.IsOpen = true;
                break;

            default:
                ChatGui.PrintError(
                    $"Unknown option '{argument}'. Try: config, calls, follow, reset, next, prev.",
                    "RaidPlan",
                    null);
                break;
        }
    }

    public void ToggleConfig() => configWindow.Toggle();

    public void ToggleMain() => mainWindow.Toggle();

    public static void SaveConfig() => PluginInterface.SavePluginConfig(Config);

    public void Dispose()
    {
        try
        {
            Plans.SaveAll();
            Learner.SaveAll();
            PluginInterface.SavePluginConfig(Config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to persist RaidPlan state on unload.");
        }

        Director.SlideRequested -= mainWindow.OnDirectedSlide;
        Director.ResetRequested -= mainWindow.OnDirectedReset;
        DutyState.DutyStarted -= OnDutyStarted;
        DutyState.DutyCompleted -= OnDutyCompleted;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMain;

        WindowSystem.RemoveAllWindows();

        mainWindow.Dispose();
        configWindow.Dispose();
        overlayWindow.Dispose();

        Director.Dispose();
        Reminders.Dispose();
        Learner.Dispose();
        Encounter.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandAlias);
    }
}
