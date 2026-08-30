using System;
using System.IO;
using System.Linq;
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
        // Backdrops are local files the person on the other end does not have, and an image would
        // blow the code past what Discord will carry anyway. The drawing travels; the tracing
        // reference it was made from does not.
        var json = JsonConvert.SerializeObject(WithoutBackdrops(document), Settings);
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

            document = PlanNormaliser.Normalise(parsed);
            return true;
        }
        catch (Exception ex)
        {
            error = "The code is damaged or incomplete — check that the whole line was copied. " + ex.Message;
            return false;
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

    /// <summary>A copy with the tracing backdrops stripped, for sharing.</summary>
    private static RaidPlanDocument WithoutBackdrops(RaidPlanDocument document)
    {
        if (document.Slides == null || !document.Slides.Any(s => s.HasBackdrop))
            return document;

        var copy = JsonConvert.DeserializeObject<RaidPlanDocument>(
            JsonConvert.SerializeObject(document, Settings), Settings)!;

        foreach (var slide in copy.Slides)
            slide.BackdropId = string.Empty;

        return copy;
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
