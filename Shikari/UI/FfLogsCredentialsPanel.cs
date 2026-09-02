using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Shikari.Services.FfLogs;
using Shikari.UI.Theme;

namespace Shikari.UI;

/// <summary>
/// The FF Logs client id and secret, with a live verdict on whether they work. Drawn from one
/// place so the Import tab and the settings window cannot drift apart.
/// </summary>
public static class FfLogsCredentialsPanel
{
    private const string ClientsUrl = "https://www.fflogs.com/api/clients/";

    public static void Draw(bool showInstructions)
    {
        var config = Plugin.Config;
        var auth = Plugin.FfLogsAuth;

        if (showInstructions)
        {
            ImGui.TextWrapped(
                "FF Logs has no anonymous API, so importing needs a client id and secret. They are " +
                "free and take a minute to make — only whoever builds the plans needs them, not the " +
                "whole static.");

            ImGui.Spacing();
            ImGui.TextDisabled("1.  Go to fflogs.com/api/clients and create a client.");
            ImGui.TextDisabled("2.  Any name will do. Put http://localhost as the redirect URL.");
            ImGui.TextDisabled("3.  Paste the id and secret below.");
            ImGui.Spacing();

            if (ImGui.Button("Copy that address", Vector2.Zero))
                ImGui.SetClipboardText(ClientsUrl);

            ImGui.Spacing();
        }

        var width = 320 * UiHelpers.Scale;

        var id = config.FfLogsClientId;
        ImGui.SetNextItemWidth(width);
        if (UiHelpers.InputTextHint("Client id##fflogs-id", "", ref id, 128))
        {
            config.FfLogsClientId = id.Trim();
            auth.Forget(config.FfLogsClientId, config.FfLogsClientSecret);
        }

        // Checked when they finish with the box rather than on every keystroke, so typing an id
        // does not fire a request per character.
        var idDone = ImGui.IsItemDeactivatedAfterEdit();

        var secret = config.FfLogsClientSecret;
        ImGui.SetNextItemWidth(width);
        if (UiHelpers.InputTextHint("Client secret##fflogs-secret", "", ref secret, 128, ImGuiInputTextFlags.Password))
        {
            config.FfLogsClientSecret = secret.Trim();
            auth.Forget(config.FfLogsClientId, config.FfLogsClientSecret);
        }

        var secretDone = ImGui.IsItemDeactivatedAfterEdit();

        if (idDone || secretDone)
        {
            Plugin.SaveConfig();

            if (!string.IsNullOrWhiteSpace(config.FfLogsClientId) &&
                !string.IsNullOrWhiteSpace(config.FfLogsClientSecret))
            {
                auth.Check(config.FfLogsClientId, config.FfLogsClientSecret);
            }
        }

        ImGui.BeginDisabled(auth.Checking);
        if (ImGui.Button("Check them", Vector2.Zero))
        {
            Plugin.SaveConfig();
            auth.Check(config.FfLogsClientId, config.FfLogsClientSecret);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Forget them", Vector2.Zero))
        {
            config.FfLogsClientId = string.Empty;
            config.FfLogsClientSecret = string.Empty;
            Plugin.FfLogs.ForgetToken();
            auth.Forget(string.Empty, string.Empty);
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.HelpMarker(
            "Stored in this plugin's config on your machine, and sent nowhere but fflogs.com. " +
            "Forgetting them clears both boxes.");

        DrawVerdict(auth);
    }

    private static void DrawVerdict(FfLogsAuth auth)
    {
        var (colour, text) = auth.State switch
        {
            CredentialState.Valid => (Palette.Vec(Palette.Good), "Connected to FF Logs."),
            CredentialState.Invalid => (Palette.Vec(Palette.Danger), auth.Message),
            CredentialState.Checking => (Palette.Vec(Palette.TextMuted), "Checking with FF Logs…"),
            CredentialState.Unchecked => (Palette.Vec(Palette.Attention), "Not checked yet."),
            _ => (Palette.Vec(Palette.TextDim), string.Empty),
        };

        if (string.IsNullOrEmpty(text))
            return;

        ImGui.Spacing();
        ImGui.TextColored(UiHelpers.Pack(colour), text);

        if (auth.State != CredentialState.Invalid || string.IsNullOrEmpty(auth.Detail))
            return;

        if (!ImGui.TreeNode("What FF Logs sent back###fflogs-auth-detail"))
            return;

        var detail = auth.Detail;
        ImGui.TextWrapped(detail.Length > 1200 ? detail[..1200] + "…" : detail);

        if (ImGui.SmallButton("Copy"))
            ImGui.SetClipboardText(detail);

        ImGui.TreePop();
    }

    /// <summary>A one-line summary for places that only need to say whether it is set up.</summary>
    public static void DrawSummary()
    {
        var auth = Plugin.FfLogsAuth;

        var (colour, text) = auth.State switch
        {
            CredentialState.Valid => (Palette.Vec(Palette.Good), "FF Logs: connected"),
            CredentialState.Invalid => (Palette.Vec(Palette.Danger), "FF Logs: " + auth.Message),
            CredentialState.Checking => (Palette.Vec(Palette.TextMuted), "FF Logs: checking…"),
            CredentialState.Unchecked => (Palette.Vec(Palette.Attention), "FF Logs: credentials not checked"),
            _ => (Palette.Vec(Palette.TextDim), "FF Logs: no credentials set"),
        };

        ImGui.TextColored(UiHelpers.Pack(colour), text);
    }
}
