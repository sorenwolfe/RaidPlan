using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RaidPlan.Services.RaidPlanIo;

/// <summary>
/// Fetches a plan's own data file, the same one the site's page loads when you open the plan.
/// </summary>
/// <remarks>
/// One request, only when somebody presses the button. No polling, no prefetching, and a code
/// already fetched this session is served from memory rather than asked for twice. The request
/// says who it is in the user agent, so the traffic is identifiable rather than anonymous.
/// </remarks>
public sealed class PlanFetcher : IDisposable
{
    /// <summary>A plan far larger than this is not a plan.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    private readonly HttpClient http;
    private readonly Dictionary<string, string> cache = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public PlanFetcher()
    {
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "RaidPlan-Dalamud/1.0 (+https://github.com/sorenwolfe/RaidPlan)");
    }

    public async Task<string> GetAsync(string code, CancellationToken cancel)
    {
        lock (gate)
        {
            if (cache.TryGetValue(code, out var hit))
                return hit;
        }

        var url = PlanUrlParser.DataUrl(code);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new PlanFetchException("No plan with that code. Check the link.");

        if (!response.IsSuccessStatusCode)
            throw new PlanFetchException($"raidplan.io returned {(int)response.StatusCode}.");

        if (response.Content.Headers.ContentLength > MaxBytes)
            throw new PlanFetchException("That plan is unreasonably large; refusing to load it.");

        var body = await ReadCapped(response, cancel).ConfigureAwait(false);

        // A redirect to the site's HTML front page is what an unknown path gives back, so a
        // non-JSON body means the code was wrong rather than the plan being broken.
        if (!body.TrimStart().StartsWith('{'))
            throw new PlanFetchException("That did not come back as plan data. Check the link.");

        lock (gate)
        {
            cache[code] = body;
        }

        return body;
    }

    private static async Task<string> ReadCapped(HttpResponseMessage response, CancellationToken cancel)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);

        var buffer = new byte[81920];
        var memory = new System.IO.MemoryStream();
        int read;

        while ((read = await stream.ReadAsync(buffer, cancel).ConfigureAwait(false)) > 0)
        {
            if (memory.Length + read > MaxBytes)
                throw new PlanFetchException("That plan is unreasonably large; refusing to load it.");

            memory.Write(buffer, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }

    public void Forget()
    {
        lock (gate)
        {
            cache.Clear();
        }
    }

    public void Dispose()
    {
        Forget();
        http.Dispose();
    }
}

public sealed class PlanFetchException : Exception
{
    public PlanFetchException(string message) : base(message)
    {
    }
}
