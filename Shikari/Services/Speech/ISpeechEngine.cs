using System;
using System.Collections.Generic;

namespace Shikari.Services.Speech;

/// <summary>Whatever actually makes the noise.</summary>
/// <remarks>
/// An interface only so the queue around it can be tested. The real one is Windows' own speech
/// service reached over COM, which cannot be exercised anywhere but a Windows desktop; everything
/// worth getting wrong — ordering, dropping, clearing, shutting down — lives in the queue and is
/// covered against a fake.
/// </remarks>
public interface ISpeechEngine : IDisposable
{
    /// <summary>Starts the engine. False means speech is unavailable and should stay quiet.</summary>
    bool Start(out string error);

    /// <summary>Speaks, and does not return until it has finished.</summary>
    void Speak(string text);

    /// <summary>Stops whatever is being said right now.</summary>
    void Stop();

    /// <summary>Rate from -10 to 10, volume from 0 to 100.</summary>
    void Configure(int rate, int volume, string voiceName);

    /// <summary>Installed voice names, for the settings list. Empty when it cannot tell.</summary>
    IReadOnlyList<string> Voices { get; }
}
