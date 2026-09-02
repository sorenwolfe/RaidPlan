using System;
using System.Text;

namespace Shikari.Services.Speech;

/// <summary>
/// Turns a call written to be read into one worth hearing.
/// </summary>
/// <remarks>
/// A banner and a voice want different things. "H2: Bell NOW" is a good banner and a poor
/// sentence, and the brackets and dashes that give a banner its shape are either read out or
/// swallowed as pauses. Nothing here changes the words — it only takes out the punctuation that
/// was there for the eye.
/// </remarks>
public static class SpokenText
{
    /// <summary>
    /// Longest line that will be spoken.
    /// </summary>
    /// <remarks>
    /// A call is useful for about as long as it takes to say. Past this it would still be talking
    /// when the next mechanic lands, so it is cut rather than allowed to run over the top of it.
    /// </remarks>
    public const int MaxLength = 140;

    /// <summary>Punctuation that shapes a banner but reads badly or not at all.</summary>
    private const string Furniture = "[]{}<>|*#_~`\"";

    public static string Tidy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var raw in text)
        {
            // Dashes and colons are pauses on screen and stumbles out loud.
            var c = raw is '—' or '–' or '-' or ':' or '\n' or '\r' or '\t' ? ' ' : raw;

            if (Furniture.IndexOf(c) >= 0)
                continue;

            if (c == ' ')
            {
                if (builder.Length == 0 || lastWasSpace)
                    continue;

                lastWasSpace = true;
                builder.Append(' ');
                continue;
            }

            lastWasSpace = false;
            builder.Append(c);
        }

        var line = builder.ToString().Trim();

        if (line.Length <= MaxLength)
            return line;

        // Cut on a word so it does not end mid-syllable.
        var cut = line.LastIndexOf(' ', Math.Min(MaxLength, line.Length - 1));
        return line[..(cut > MaxLength / 2 ? cut : MaxLength)].Trim();
    }

    /// <summary>
    /// The line to speak for a call.
    /// </summary>
    /// <remarks>
    /// The subline lists what everyone else is pressing. That is useful to glance at and useless
    /// to listen to while dodging, so it is left out unless it is asked for.
    /// </remarks>
    public static string For(string? headline, string? subline, bool includeOthers)
    {
        var main = Tidy(headline);

        if (!includeOthers)
            return main;

        var rest = Tidy(subline);
        if (rest.Length == 0)
            return main;

        return main.Length == 0 ? rest : Tidy(main + ", " + rest);
    }
}
