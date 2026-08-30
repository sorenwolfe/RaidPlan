using System.Text.RegularExpressions;

namespace RaidPlan.Services.RaidPlanIo;

/// <summary>The plan code pulled out of whatever the player pasted.</summary>
public readonly record struct PlanUrl(string Code)
{
    public bool IsValid => Code.Length > 0;
}

/// <summary>
/// Finds the plan code in a raidplan.io link. Players paste the address bar, a share link, or
/// sometimes just the code, so all three are accepted.
/// </summary>
public static class PlanUrlParser
{
    /// <summary>Codes seen in the wild are 16 characters of URL-safe base64.</summary>
    private const string CodeChars = @"[A-Za-z0-9_\-]{10,32}";

    // /plan/ is required rather than optional. A plan link always has it, and demanding it is
    // what keeps every other page on the site from looking like a code.
    private static readonly Regex FromLink = new(
        @"raidplan\.io/plan/(?<code>" + CodeChars + @")(?![A-Za-z0-9_\-])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BareCode = new(
        @"^\s*(?<code>" + CodeChars + @")\s*$",
        RegexOptions.Compiled);

    public static PlanUrl Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new PlanUrl(string.Empty);

        var text = input.Trim();

        var link = FromLink.Match(text);
        if (link.Success)
            return new PlanUrl(link.Groups["code"].Value);

        var bare = BareCode.Match(text);
        if (bare.Success)
            return new PlanUrl(bare.Groups["code"].Value);

        return new PlanUrl(string.Empty);
    }

    /// <summary>Where the plan's own data lives. The same file the site's page loads.</summary>
    public static string DataUrl(string code) => $"https://userdata.raidplan.io/{code}.json";
}
