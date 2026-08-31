using System;
using System.Collections.Generic;
using System.Reflection;

namespace RaidPlan.Services.Speech;

/// <summary>
/// Windows' own speech service, reached over COM.
/// </summary>
/// <remarks>
/// By reflection rather than a package reference, deliberately: it means no extra assembly rides
/// along in the plugin, nothing to go stale against a future Dalamud, and a machine without SAPI
/// fails at the first call with a message instead of failing to load the plugin at all.
///
/// Every call is wrapped. A voice engine is not worth a crash — the worst it may do is go quiet.
/// </remarks>
public sealed class SapiSpeechEngine : ISpeechEngine
{
    private const int SpeakAsync = 1;
    private const int SpeakPurgeBeforeSpeak = 2;

    private object? voice;
    private readonly List<string> voices = new();

    public IReadOnlyList<string> Voices => voices;

    public bool Start(out string error)
    {
        error = string.Empty;

        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (type == null)
            {
                error = "Windows speech (SAPI) is not available on this machine.";
                return false;
            }

            voice = Activator.CreateInstance(type);
            if (voice == null)
            {
                error = "Windows speech could not be started.";
                return false;
            }

            ReadVoiceNames();
            return true;
        }
        catch (Exception ex)
        {
            error = "Windows speech could not be started: " + ex.Message;
            voice = null;
            return false;
        }
    }

    public void Speak(string text)
    {
        if (voice == null || string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            // Synchronous on purpose. The queue outside owns the ordering, and letting SAPI hold
            // its own backlog would mean Stop could not clear one without clearing the other.
            Invoke(voice, "Speak", text, 0);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Speech failed.");
        }
    }

    public void Stop()
    {
        if (voice == null)
            return;

        try
        {
            // Speaking nothing with the purge flag is SAPI's way of cutting the current line off.
            Invoke(voice, "Speak", string.Empty, SpeakAsync | SpeakPurgeBeforeSpeak);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not stop speech.");
        }
    }

    public void Configure(int rate, int volume, string voiceName)
    {
        if (voice == null)
            return;

        try
        {
            SetProperty(voice, "Rate", Math.Clamp(rate, -10, 10));
            SetProperty(voice, "Volume", Math.Clamp(volume, 0, 100));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not set the speech rate or volume.");
        }

        if (string.IsNullOrWhiteSpace(voiceName))
            return;

        try
        {
            var tokens = Invoke(voice, "GetVoices", string.Empty, string.Empty);
            if (tokens == null)
                return;

            var count = Convert.ToInt32(GetProperty(tokens, "Count") ?? 0);
            for (var i = 0; i < count; i++)
            {
                var token = Invoke(tokens, "Item", i);
                if (token == null)
                    continue;

                var name = Invoke(token, "GetDescription", 0) as string ?? string.Empty;
                if (!name.Equals(voiceName, StringComparison.OrdinalIgnoreCase))
                    continue;

                SetProperty(voice, "Voice", token);
                return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not select the speech voice.");
        }
    }

    private void ReadVoiceNames()
    {
        voices.Clear();

        try
        {
            var tokens = Invoke(voice!, "GetVoices", string.Empty, string.Empty);
            if (tokens == null)
                return;

            var count = Convert.ToInt32(GetProperty(tokens, "Count") ?? 0);
            for (var i = 0; i < count; i++)
            {
                var token = Invoke(tokens, "Item", i);
                if (token != null && Invoke(token, "GetDescription", 0) is string name && name.Length > 0)
                    voices.Add(name);
            }
        }
        catch (Exception ex)
        {
            // A machine that will not list its voices can still speak with the default one.
            Plugin.Log.Warning(ex, "Could not list the installed voices.");
        }
    }

    private static object? Invoke(object target, string method, params object?[] args) =>
        target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);

    private static object? GetProperty(object target, string name) =>
        target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);

    private static void SetProperty(object target, string name, object value) =>
        target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, new[] { value });

    public void Dispose()
    {
        Stop();

        if (voice == null)
            return;

        try
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(voice))
                System.Runtime.InteropServices.Marshal.ReleaseComObject(voice);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not release the speech object.");
        }

        voice = null;
    }
}
