using System;
using System.Collections.Generic;
using System.Threading;

namespace RaidPlan.Services.Speech;

/// <summary>
/// Says the calls out loud, on its own thread.
/// </summary>
/// <remarks>
/// Speaking blocks for as long as the line takes, which is far too long to do on the frame the
/// call fires. So calls go into a queue and one worker thread reads it.
///
/// The engine is created on that same worker thread and never touched from anywhere else. It is a
/// COM object, and one created on the game's frame thread but driven from a worker is a
/// cross-apartment call — which may work, may be slow, and may not work at all depending on how
/// the machine has SAPI registered. Keeping it on one thread removes the question.
///
/// The queue is deliberately short. A raid call is only useful in the second or two it applies to,
/// so when calls arrive faster than they can be spoken the oldest is dropped rather than the
/// newest: hearing what is happening now, late, beats hearing what happened ten seconds ago on
/// time. A wipe clears the backlog outright.
/// </remarks>
public sealed class SpeechChannel : IDisposable
{
    /// <summary>Lines allowed to stack up before the oldest starts being dropped.</summary>
    public const int MaxQueued = 3;

    private readonly object gate = new();
    private readonly Queue<string> queue = new();
    private readonly ISpeechEngine engine;
    private readonly SemaphoreSlim signal = new(0);

    private Thread? worker;
    private volatile bool running;
    private volatile bool starting;
    private volatile bool ready;

    private int wantRate;
    private int wantVolume = 90;
    private string wantVoice = string.Empty;
    private volatile bool settingsDirty;

    /// <summary>Lines dropped because they arrived faster than they could be said.</summary>
    public int Dropped { get; private set; }

    public SpeechChannel(ISpeechEngine engine) => this.engine = engine;

    /// <summary>Empty until something goes wrong, then why speech is not working.</summary>
    public string Error { get; private set; } = string.Empty;

    /// <summary>True once the engine is up and speech can actually be heard.</summary>
    public bool Available => ready;

    /// <summary>The engine is coming up. Only for telling the player, so they see something.</summary>
    public bool Starting => starting && !ready && Error.Length == 0;

    public IReadOnlyList<string> Voices => ready ? engine.Voices : Array.Empty<string>();

    /// <summary>
    /// Brings the engine up, on its own thread. Safe to call every frame; only the first does
    /// anything.
    /// </summary>
    /// <remarks>
    /// Returns straight away — the engine may take a moment, and none of that happens on the
    /// caller's thread. Anything said before it is ready is dropped rather than queued, which is
    /// right for raid calls: a call from four seconds ago is not worth hearing now.
    /// </remarks>
    public void Start()
    {
        lock (gate)
        {
            if (starting)
                return;

            starting = true;
            running = true;

            worker = new Thread(Pump)
            {
                IsBackground = true,
                Name = "RaidPlan speech",
            };

            worker.Start();
        }
    }

    /// <summary>Remembers the rate, volume and voice, and has the worker apply them.</summary>
    public void Configure(int rate, int volume, string voiceName)
    {
        lock (gate)
        {
            wantRate = rate;
            wantVolume = volume;
            wantVoice = voiceName ?? string.Empty;
        }

        settingsDirty = true;
        Wake();
    }

    /// <summary>Queues a line. Does nothing at all if speech is not up.</summary>
    public void Say(string text)
    {
        if (!ready || string.IsNullOrWhiteSpace(text))
            return;

        lock (gate)
        {
            while (queue.Count >= MaxQueued)
            {
                queue.Dequeue();
                Dropped++;
            }

            queue.Enqueue(text.Trim());
        }

        Wake();
    }

    /// <summary>Drops anything waiting and cuts off whatever is being said. For a wipe.</summary>
    public void Clear()
    {
        if (!ready)
            return;

        lock (gate)
            queue.Clear();

        engine.Stop();
    }

    private void Wake()
    {
        try
        {
            signal.Release();
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
    }

    private void Pump()
    {
        if (!engine.Start(out var error))
        {
            Error = error.Length > 0 ? error : "Speech could not be started.";
            running = false;
            return;
        }

        ApplySettings();
        ready = true;

        while (running)
        {
            try
            {
                signal.Wait();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (!running)
                return;

            if (settingsDirty)
                ApplySettings();

            string? line = null;
            lock (gate)
            {
                if (queue.Count > 0)
                    line = queue.Dequeue();
            }

            if (line != null)
                engine.Speak(line);
        }
    }

    private void ApplySettings()
    {
        int rate, volume;
        string voice;

        lock (gate)
        {
            rate = wantRate;
            volume = wantVolume;
            voice = wantVoice;
        }

        settingsDirty = false;
        engine.Configure(rate, volume, voice);
    }

    public void Dispose()
    {
        // Order matters. Stop the loop, cut off the line being spoken so the worker is not stuck
        // inside the engine, wake it so it can see it should leave, then wait — and only then
        // take the engine away from under it.
        running = false;
        ready = false;

        lock (gate)
            queue.Clear();

        try
        {
            engine.Stop();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not stop speech on unload.");
        }

        Wake();

        if (worker != null && worker.IsAlive)
            worker.Join(TimeSpan.FromSeconds(2));

        worker = null;

        engine.Dispose();
        signal.Dispose();
        starting = false;
    }
}
