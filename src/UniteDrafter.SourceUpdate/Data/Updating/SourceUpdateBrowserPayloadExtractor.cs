using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace UniteDrafter.SourceUpdate.Data.Updating;

internal static class SourceUpdateBrowserPayloadExtractor
{
    private const string UniteApiOrigin = "https://uniteapi.dev";

    public static ConcurrentQueue<BrowserResponseCapture> AttachResponseCapture(IPage page)
    {
        var recentResponseBodies = new ConcurrentQueue<BrowserResponseCapture>();

        page.Response += async (_, response) =>
        {
            try
            {
                if (!response.Url.StartsWith(UniteApiOrigin, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var contentType = response.Headers.TryGetValue("content-type", out var headerValue)
                    ? headerValue
                    : string.Empty;

                if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                    && !response.Url.Contains("_next", StringComparison.OrdinalIgnoreCase)
                    && !response.Url.Contains("api", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                recentResponseBodies.Enqueue(new BrowserResponseCapture(
                    response.Url,
                    contentType,
                    await response.TextAsync()));

                while (recentResponseBodies.Count > 50)
                {
                    recentResponseBodies.TryDequeue(out BrowserResponseCapture? _);
                }
            }
            catch
            {
            }
        };

        return recentResponseBodies;
    }

    public static async Task<string> GetPagePayloadAsync(
        IPage page,
        string url,
        ConcurrentQueue<BrowserResponseCapture> recentResponseBodies,
        Func<IPage, string, Func<Task<bool>>, string, ISourceUpdateReporter, CancellationToken, Task<string>> getUsableHtmlAsync,
        ISourceUpdateReporter reporter,
        CancellationToken cancellationToken)
    {
        while (recentResponseBodies.TryDequeue(out _))
        {
        }

        var html = await getUsableHtmlAsync(
            page,
            url,
            async () =>
            {
                var payload = await TryExtractPayloadAsync(page, recentResponseBodies, cancellationToken);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return false;
                }

                try
                {
                    SourceUpdatePayloadInspector.EnsurePayloadContainsCounters(url, payload);
                    return true;
                }
                catch
                {
                    return false;
                }
            },
            $"Waiting for a usable guide payload from {url}. If Cloudflare is shown in Edge, complete it there; the updater will keep retrying automatically.",
            reporter,
            cancellationToken);

        var extractedPayload = await TryExtractPayloadAsync(page, recentResponseBodies, cancellationToken);
        if (string.IsNullOrWhiteSpace(extractedPayload))
        {
            await OpenCountersTabAsync(page, cancellationToken);
            extractedPayload = await TryExtractPayloadAsync(page, recentResponseBodies, cancellationToken);
        }

        return string.IsNullOrWhiteSpace(extractedPayload) ? html : extractedPayload;
    }

    public static async Task<string?> TryExtractPayloadAsync(
        IPage page,
        ConcurrentQueue<BrowserResponseCapture> recentResponseBodies,
        CancellationToken cancellationToken)
    {
        var candidatePayloads = new List<string>();

        void AddCandidate(string? text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                candidatePayloads.Add(text);
            }
        }

        AddCandidate(await page.ContentAsync());

        try
        {
            var nextDataJson = await page.EvaluateAsync<string?>(
                @"() => {
                    const nextData = document.querySelector('#__NEXT_DATA__');
                    return nextData ? nextData.textContent : null;
                }");
            AddCandidate(nextDataJson);
        }
        catch
        {
        }

        try
        {
            var windowStateJson = await page.EvaluateAsync<string?>(
                @"() => {
                    const candidates = [window.__NEXT_DATA__, window.__NUXT__, window.__INITIAL_STATE__];
                    for (const candidate of candidates) {
                        if (candidate) {
                            return JSON.stringify(candidate);
                        }
                    }
                    return null;
                }");
            AddCandidate(windowStateJson);
        }
        catch
        {
        }

        foreach (var response in recentResponseBodies.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddCandidate(response.Body);
        }

        foreach (var candidate in candidatePayloads)
        {
            var extractedJson = SourceUpdatePayloadInspector.TryExtractPageJson(candidate);
            if (!string.IsNullOrWhiteSpace(extractedJson)
                && SourceUpdatePayloadInspector.PayloadHasCounters(extractedJson))
            {
                return extractedJson;
            }

            if (SourceUpdatePayloadInspector.LooksLikeJson(candidate)
                && SourceUpdatePayloadInspector.PayloadHasCounters(candidate))
            {
                return candidate.Trim();
            }
        }

        return null;
    }

    public static async Task NavigateAsync(IPage page, string url)
    {
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = 10000
            });
        }
        catch (TimeoutException)
        {
            // Some pages keep network requests alive. DOM content is enough for our extraction.
        }
    }

    private static async Task OpenCountersTabAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            var countersTab = page.GetByText("Counters", new PageGetByTextOptions
            {
                Exact = true
            }).First;

            if (!await countersTab.IsVisibleAsync())
            {
                return;
            }

            await countersTab.ClickAsync();
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        catch
        {
            // Older pages already expose the payload without a tab click.
        }
    }
}
