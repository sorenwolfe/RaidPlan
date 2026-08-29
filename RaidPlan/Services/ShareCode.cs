using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using RaidPlan.Model;

namespace RaidPlan.Services;

/// <summary>
/// Turns a plan into a single line of text that survives Discord, and back again.
/// Format: <c>RPLAN1:{base64url of gzipped UTF-8 JSON}</c>.
/// </summary>
public static class ShareCode
{
    private const string Prefix = "RPLAN1:";

    private static readonly JsonSerializerSettings Settings = PlanJson.Compact();

    public static string Encode(RaidPlanDocument document)
    {
        var json = JsonConvert.SerializeObject(document, Settings);
        var raw = Encoding.UTF8.GetBytes(json);

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(raw, 0, raw.Length);
        }

        return Prefix + ToBase64Url(output.ToArray());
    }

    public static bool TryDecode(string? code, out RaidPlanDocument? document, out string error)
    {
        document = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            error = "Nothing to import — the code is empty.";
            return false;
        }

        var cleaned = Clean(code);
        if (cleaned.Length == 0)
        {
            error = "That does not look like a RaidPlan code.";
            return false;
        }

        try
        {
            var compressed = FromBase64Url(cleaned);

            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            var json = reader.ReadToEnd();

            var parsed = JsonConvert.DeserializeObject<RaidPlanDocument>(json, Settings);
            if (parsed == null)
            {
                error = "The code unpacked but held no plan.";
                return false;
            }

            if (parsed.FormatVersion > RaidPlanDocument.CurrentFormatVersion)
            {
                error = $"This plan was made with a newer version of RaidPlan (format {parsed.FormatVersion}). Update the plugin first.";
                return false;
            }

            Normalise(parsed);
            document = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = "The code is damaged or incomplete — check that the whole line was copied. " + ex.Message;
            return false;
        }
    }

    /// <summary>Fills in anything an older or hand-edited plan might be missing.</summary>
    private static void Normalise(RaidPlanDocument doc)
    {
        doc.Arena ??= new ArenaSettings();
        doc.Roster ??= new();
        doc.Slides ??= new();
        doc.Timeline ??= new();

        if (doc.Slides.Count == 0)
            doc.Slides.Add(new Slide { Title = "Slide 1" });

        if (doc.Roster.Count == 0)
        {
            var template = RaidPlanDocument.CreateDefault();
            doc.Roster = template.Roster;
        }

        foreach (var slide in doc.Slides)
            slide.Items ??= new();

        foreach (var entry in doc.Timeline)
        {
            entry.Assignments ??= new();
            entry.SlotCallText ??= new();
        }
    }

    private static string Clean(string code)
    {
        var span = code.Trim();
        var idx = span.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            span = span[(idx + Prefix.Length)..];

        var sb = new StringBuilder(span.Length);
        foreach (var c in span)
        {
            if (char.IsWhiteSpace(c))
                continue;
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string ToBase64Url(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        return Convert.FromBase64String(s);
    }
}
