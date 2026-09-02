using System;
using System.Text.RegularExpressions;

namespace Shikari.Services.FfLogs;

/// <summary>A report code, and optionally which fight in it, pulled out of a pasted link.</summary>
public readonly record struct ReportRef(string Code, int? FightId)
{
    public bool IsValid => !string.IsNullOrEmpty(Code);
}

/// <summary>
/// Pulls the report code and fight number out of whatever someone pasted. People paste the whole
/// URL, sometimes with a fight anchor, sometimes just the code.
/// </summary>
public static class ReportUrl
{
    // Report codes are 16 alphanumeric characters.
    private static readonly Regex CodePattern = new(
        @"(?:reports/)?(?<code>[a-zA-Z0-9]{16})",
        RegexOptions.Compiled);

    private static readonly Regex FightPattern = new(
        @"fight=(?<fight>\d+|last)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Fight id meaning "whichever fight was last in the report".</summary>
    public const int LastFight = -1;

    public static ReportRef Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return default;

        var text = input.Trim();

        var codeMatch = CodePattern.Match(text);
        if (!codeMatch.Success)
            return default;

        var code = codeMatch.Groups["code"].Value;

        int? fight = null;
        var fightMatch = FightPattern.Match(text);
        if (fightMatch.Success)
        {
            var value = fightMatch.Groups["fight"].Value;
            fight = value.Equals("last", StringComparison.OrdinalIgnoreCase)
                ? LastFight
                : int.TryParse(value, out var parsed) ? parsed : null;
        }

        return new ReportRef(code, fight);
    }
}
