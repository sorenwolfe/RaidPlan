using System;
using System.Threading;
using System.Threading.Tasks;

namespace RaidPlan.Services.FfLogs;

public enum CredentialState
{
    /// <summary>Nothing entered yet.</summary>
    Unset = 0,

    /// <summary>Entered, but never tried against FF Logs.</summary>
    Unchecked = 1,

    Checking = 2,
    Valid = 3,
    Invalid = 4,
}

/// <summary>
/// Knows whether the FF Logs credentials actually work. Checked when they are typed in rather
/// than on the first import, so a typo is caught while the player is still looking at the box
/// they typed it into.
/// </summary>
public sealed class FfLogsAuth
{
    private readonly object gate = new();

    /// <summary>
    /// Bumped on every edit and every new check. A reply carrying an old number is a reply to a
    /// question nobody is asking any more — the player has typed something since — so it is
    /// dropped rather than allowed to overwrite a newer answer.
    /// </summary>
    private int generation;

    public CredentialState State { get; private set; } = CredentialState.Unset;

    public string Message { get; private set; } = string.Empty;

    /// <summary>Raw response body from a failure, for the "what came back" disclosure.</summary>
    public string Detail { get; private set; } = string.Empty;

    public bool Checking => State == CredentialState.Checking;

    public bool Usable => State is CredentialState.Valid or CredentialState.Unchecked;

    /// <summary>Whether a reply from a check should still be applied.</summary>
    public static bool StillWanted(int replyGeneration, int currentGeneration) =>
        replyGeneration == currentGeneration;

    /// <summary>Works out the resting state for a pair of credentials that has not been checked.</summary>
    public static CredentialState Resting(string? clientId, string? clientSecret) =>
        string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)
            ? CredentialState.Unset
            : CredentialState.Unchecked;

    /// <summary>Called when either field is edited: whatever we knew is now out of date.</summary>
    public void Forget(string? clientId, string? clientSecret)
    {
        lock (gate)
        {
            generation++;
            State = Resting(clientId, clientSecret);
            Message = string.Empty;
            Detail = string.Empty;
        }
    }

    /// <summary>Asks FF Logs for a token, purely to find out whether the credentials are good.</summary>
    public void Check(string clientId, string clientSecret)
    {
        int mine;

        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                generation++;
                State = CredentialState.Unset;
                Message = "Both an id and a secret are needed.";
                Detail = string.Empty;
                return;
            }

            mine = ++generation;
            State = CredentialState.Checking;
            Message = "Checking with FF Logs…";
            Detail = string.Empty;
        }

        var cancel = Plugin.Shutdown;

        Task.Run(async () =>
        {
            try
            {
                // A fresh token, not whatever is cached, or a corrected secret would look fine
                // purely because the old one still had time on it.
                Plugin.FfLogs.ForgetToken();
                await Plugin.FfLogs.GetTokenAsync(clientId, clientSecret, cancel).ConfigureAwait(false);

                Apply(mine, CredentialState.Valid, "These credentials work.", string.Empty);
            }
            catch (OperationCanceledException)
            {
                // Unloading. Nothing left to tell.
            }
            catch (FfLogsException ex)
            {
                Apply(mine, CredentialState.Invalid, ex.Message, ex.Detail ?? string.Empty);
            }
            catch (Exception ex)
            {
                Apply(mine, CredentialState.Invalid, "Could not reach FF Logs: " + ex.Message, string.Empty);
            }
        }, cancel);
    }

    private void Apply(int mine, CredentialState state, string message, string detail)
    {
        lock (gate)
        {
            if (!StillWanted(mine, generation))
                return;

            State = state;
            Message = message;
            Detail = detail;
        }
    }
}
