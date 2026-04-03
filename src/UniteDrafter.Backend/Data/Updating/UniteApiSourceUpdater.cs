using System.Collections.Concurrent;
using System.Net;
using Microsoft.Playwright;

namespace UniteDrafter.Data.Updating;

public sealed record SourceUpdateOptions(
    string OutputDirectory,
    IReadOnlyList<string> Targets,
    string? CookieHeader,
    bool UseBrowser,
    bool Headless,
    string BrowserProfileDirectory);

public sealed record SourceUpdateFailure(string Target, string Error);

public sealed record SourceUpdateSummary(
    string OutputDirectory,
    int SavedFiles,
    int FailedFiles,
    IReadOnlyList<string> DiscoveredUrls,
    IReadOnlyList<SourceUpdateFailure> Failures);

internal sealed record BrowserResponseCapture(string Url, string ContentType, string Body);

public static class UniteApiSourceUpdater
{
    private const string UniteApiOrigin = "https://uniteapi.dev";
    private static bool browserChallengePromptShown;

    public static IReadOnlyList<string> ExtractGuideUrls(string html) =>
        SourceUpdateTargetResolver.ExtractGuideUrls(html);

    public static string? TryExtractPageJson(string responseText) =>
        SourceUpdatePayloadInspector.TryExtractPageJson(responseText);

    public static async Task<SourceUpdateSummary> UpdateAsync(SourceUpdateOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);

        Directory.CreateDirectory(options.OutputDirectory);

        if (options.UseBrowser)
        {
            return await UpdateWithBrowserAsync(options, cancellationToken);
        }

        using var httpClient = CreateClient(options.CookieHeader);
        var urls = await SourceUpdateTargetResolver.ResolveTargetUrlsAsync(httpClient, options.Targets, cancellationToken);
        var failures = new List<SourceUpdateFailure>();
        var savedFiles = 0;

        foreach (var url in urls)
        {
            string? responseText = null;
            try
            {
                responseText = await httpClient.GetStringAsync(url, cancellationToken);
                var extractedJson = SourceUpdatePayloadInspector.TryExtractPageJson(responseText);
                if (string.IsNullOrWhiteSpace(extractedJson))
                {
                    throw SourceUpdatePayloadInspector.BuildFetchException(url, responseText);
                }

                SourceUpdatePayloadInspector.EnsurePayloadContainsCounters(url, extractedJson);

                var outputFileName = Path.GetFileName(new Uri(url).AbsolutePath) + ".json";
                var outputPath = Path.Combine(options.OutputDirectory, outputFileName);
                await File.WriteAllTextAsync(outputPath, extractedJson, cancellationToken);
                savedFiles++;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    SourceUpdateDiagnostics.WriteFailureDiagnostics(url, responseText);
                }

                failures.Add(new SourceUpdateFailure(url, ex.Message));
            }
        }

        return new SourceUpdateSummary(
            options.OutputDirectory,
            savedFiles,
            failures.Count,
            urls,
            failures);
    }

    private static async Task<SourceUpdateSummary> UpdateWithBrowserAsync(SourceUpdateOptions options, CancellationToken cancellationToken)
    {
        var failures = new List<SourceUpdateFailure>();
        var savedFiles = 0;

        Directory.CreateDirectory(options.BrowserProfileDirectory);

        using var playwright = await Playwright.CreateAsync();
        await using var context = await playwright.Chromium.LaunchPersistentContextAsync(
            options.BrowserProfileDirectory,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = options.Headless,
                ExecutablePath = ResolveEdgePath(),
                ViewportSize = null,
                Args =
                [
                    "--disable-blink-features=AutomationControlled"
                ]
            });

        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(60000);
        page.SetDefaultTimeout(60000);
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

        Console.WriteLine("Browser mode is active.");
        Console.WriteLine($"Using Edge profile at: {options.BrowserProfileDirectory}");
        Console.WriteLine("If Cloudflare appears in the browser, complete the check there and come back to the terminal if prompted.");
        browserChallengePromptShown = false;

        var urls = await SourceUpdateTargetResolver.ResolveTargetUrlsWithBrowserAsync(page, options.Targets, GetUsableHtmlAsync, cancellationToken);
        foreach (var url in urls)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var responseText = await GetPagePayloadWithBrowserAsync(page, url, recentResponseBodies, cancellationToken);
                var extractedJson = SourceUpdatePayloadInspector.TryExtractPageJson(responseText);
                if (string.IsNullOrWhiteSpace(extractedJson))
                {
                    throw SourceUpdatePayloadInspector.BuildFetchException(url, responseText);
                }

                SourceUpdatePayloadInspector.EnsurePayloadContainsCounters(url, extractedJson);

                var outputFileName = Path.GetFileName(new Uri(url).AbsolutePath) + ".json";
                var outputPath = Path.Combine(options.OutputDirectory, outputFileName);
                await File.WriteAllTextAsync(outputPath, extractedJson, cancellationToken);
                savedFiles++;

                Console.WriteLine($"Saved {outputFileName}");
            }
            catch (Exception ex)
            {
                SourceUpdateDiagnostics.WriteFailureDiagnostics(url, await page.ContentAsync());
                SourceUpdateDiagnostics.WriteResponseDiagnostics(url, recentResponseBodies.ToArray());
                failures.Add(new SourceUpdateFailure(url, ex.Message));
            }
        }

        return new SourceUpdateSummary(
            options.OutputDirectory,
            savedFiles,
            failures.Count,
            urls,
            failures);
    }

    internal static async Task<string> GetUsableHtmlAsync(
        IPage page,
        string url,
        Func<Task<bool>> isUsable,
        string prompt,
        CancellationToken cancellationToken)
    {
        await NavigateAsync(page, url);
        var html = await page.ContentAsync();
        if (await isUsable())
        {
            return html;
        }

        if (!SourceUpdatePayloadInspector.IsLikelyCloudflareChallenge(html))
        {
            throw SourceUpdatePayloadInspector.BuildFetchException(url, html);
        }

        if (!browserChallengePromptShown)
        {
            Console.WriteLine(prompt);
            browserChallengePromptShown = true;
        }

        for (var attempt = 0; attempt < 15; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            await NavigateAsync(page, url);
            html = await page.ContentAsync();
            if (await isUsable())
            {
                return html;
            }
        }

        throw SourceUpdatePayloadInspector.BuildFetchException(url, html);
    }

    private static HttpClient CreateClient(string? cookieHeader)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/json;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", UniteApiOrigin + "/");

        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        return client;
    }

    private static async Task<string> GetPagePayloadWithBrowserAsync(
        IPage page,
        string url,
        ConcurrentQueue<BrowserResponseCapture> recentResponseBodies,
        CancellationToken cancellationToken)
    {
        while (recentResponseBodies.TryDequeue(out _))
        {
        }

        var html = await GetUsableHtmlAsync(
            page,
            url,
            async () =>
            {
                var payload = await TryExtractPayloadFromBrowserAsync(page, recentResponseBodies, cancellationToken);
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
            cancellationToken);

        var payload = await TryExtractPayloadFromBrowserAsync(page, recentResponseBodies, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            await OpenCountersTabAsync(page, cancellationToken);
            payload = await TryExtractPayloadFromBrowserAsync(page, recentResponseBodies, cancellationToken);
        }

        return string.IsNullOrWhiteSpace(payload) ? html : payload;
    }

    private static async Task<string?> TryExtractPayloadFromBrowserAsync(
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

    private static async Task NavigateAsync(IPage page, string url)
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

    private static string ResolveEdgePath()
    {
        var candidates = new[]
        {
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not find Microsoft Edge. Install Edge or adjust the updater to use another Chromium browser.");
    }
}
