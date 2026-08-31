using System;
using System.Collections.Concurrent;
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
    private volatile bool started;

    /// <summary>Lines dropped because they arrived faster than they could be said.</summary>
    public int Dropped { get; private set; }

    public SpeechChannel(ISpeechEngine engine) => this.engine = engine;

    /// <summary>Empty until something goes wrong, then why speech is not working.</summary>
    public string Error { get; private set; } = string.Empty;

    /// <summary>True once the engine has started and speech can actually be heard.</summary>
    public bool Available => started && Error.Length == 0;

    public IReadOnlyList<string> Voices => engine.Voices;

    /// <summary>
    /// Starts the engine and its thread. Safe to call again; only the first call does anything.
    /// </summary>
    /// <remarks>
    /// Started on demand rather than at load, so a player who never turns speech on never pays
    /// for a COM object and a thread they did not ask for.
    /// </remarks>
    public bool Start()
    {
        lock (gate)
        {
            if (started || Error.Length > 0)
                return Available;

            if (!engine.Start(out var error))
            {
                Error = error;
                return false;
            }

            started = true;
            running = true;

            worker = new Thread(Pump)
            {
                IsBackground = true,
                Name = "RaidPlan speech",
            };

            worker.Start();
            return true;
        }
    }

    public void Configure(int rate, int volume, string voiceName)
    {
        if (started)
            engine.Configure(rate, volume, voiceName);
    }

    /// <summary>Queues a line. Does nothing at all if speech never started.</summary>
    public void Say(string text)
    {
        if (!Available || string.IsNullOrWhiteSpace(text))
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

        signal.Release();
    }

    /// <summary>Drops anything waiting and cuts off whatever is being said. For a wipe.</summary>
    public void Clear()
    {
        if (!started)
            return;

        lock (gate)
            queue.Clear();

        engine.Stop();
    }

    private void Pump()
    {
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

    public void Dispose()
    {
        // Order matters. Stop the loop, cut off the line being spoken so the worker is not stuck
        // inside the engine, wake it so it can see it should leave, then wait — and only then
        // take the engine away from under it.
        running = false;

        lock (gate)
            queue.Clear();

        if (started)
        {
            try
            {
                engine.Stop();
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Could not stop speech on unload.");
            }
        }

        try
        {
            signal.Release();
        }
        catch (ObjectDisposedException)
        {
            // Already gone.
        }

        if (worker != null && worker.IsAlive)
            worker.Join(TimeSpan.FromSeconds(2));

        worker = null;

        engine.Dispose();
        signal.Dispose();
        started = false;
    }
}
