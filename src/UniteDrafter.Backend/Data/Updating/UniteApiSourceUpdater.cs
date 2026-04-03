using System.Net;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using DecrypterService = UniteDrafter.Decrypter.Decrypter;
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

public static partial class UniteApiSourceUpdater
{
    private const string UniteApiOrigin = "https://uniteapi.dev";
    private const string PokemonIndexUrl = $"{UniteApiOrigin}/pokemon";
    private const string GuidePathPrefix = "/pokemon/best-builds-movesets-and-guide-for-";
    private const string FailureDiagnosticsDirectory = "data/Database/Diagnostics/SourceUpdateFailures";
    private static bool browserChallengePromptShown;

    public static async Task<SourceUpdateSummary> UpdateAsync(SourceUpdateOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);

        Directory.CreateDirectory(options.OutputDirectory);

        if (options.UseBrowser)
        {
            return await UpdateWithBrowserAsync(options, cancellationToken);
        }

        using var httpClient = CreateClient(options.CookieHeader);
        var urls = await ResolveTargetUrlsAsync(httpClient, options.Targets, cancellationToken);
        var failures = new List<SourceUpdateFailure>();
        var savedFiles = 0;

        foreach (var url in urls)
        {
            string? responseText = null;
            try
            {
                responseText = await httpClient.GetStringAsync(url, cancellationToken);
                var extractedJson = TryExtractPageJson(responseText);
                if (string.IsNullOrWhiteSpace(extractedJson))
                {
                    throw BuildFetchException(url, responseText);
                }

                EnsurePayloadContainsCounters(url, extractedJson);

                var outputFileName = Path.GetFileName(new Uri(url).AbsolutePath) + ".json";
                var outputPath = Path.Combine(options.OutputDirectory, outputFileName);
                await File.WriteAllTextAsync(outputPath, extractedJson, cancellationToken);
                savedFiles++;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    WriteFailureDiagnostics(url, responseText);
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

        var urls = await ResolveTargetUrlsWithBrowserAsync(page, options.Targets, cancellationToken);
        foreach (var url in urls)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var responseText = await GetPagePayloadWithBrowserAsync(page, url, recentResponseBodies, cancellationToken);
                var extractedJson = TryExtractPageJson(responseText);
                if (string.IsNullOrWhiteSpace(extractedJson))
                {
                    throw BuildFetchException(url, responseText);
                }

                EnsurePayloadContainsCounters(url, extractedJson);

                var outputFileName = Path.GetFileName(new Uri(url).AbsolutePath) + ".json";
                var outputPath = Path.Combine(options.OutputDirectory, outputFileName);
                await File.WriteAllTextAsync(outputPath, extractedJson, cancellationToken);
                savedFiles++;

                Console.WriteLine($"Saved {outputFileName}");
            }
            catch (Exception ex)
            {
                WriteFailureDiagnostics(url, await page.ContentAsync());
                WriteResponseDiagnostics(url, recentResponseBodies.ToArray());
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

    public static IReadOnlyList<string> ExtractGuideUrls(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        return GuideUrlRegex()
            .Matches(html)
            .Select(match => match.Groups["url"].Value)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? TryExtractPageJson(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var trimmed = responseText.Trim();
        if (LooksLikeJson(trimmed))
        {
            return trimmed;
        }

        var nextDataMatch = NextDataScriptRegex().Match(responseText);
        if (!nextDataMatch.Success)
        {
            return null;
        }

        var json = WebUtility.HtmlDecode(nextDataMatch.Groups["json"].Value).Trim();
        return LooksLikeJson(json) ? json : null;
    }

    private static async Task<IReadOnlyList<string>> ResolveTargetUrlsAsync(
        HttpClient httpClient,
        IReadOnlyList<string> targets,
        CancellationToken cancellationToken)
    {
        if (targets.Count > 0)
        {
            return targets.Select(ToGuideUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var rosterHtml = await httpClient.GetStringAsync(PokemonIndexUrl, cancellationToken);
        var discoveredUrls = ExtractGuideUrls(rosterHtml);
        if (discoveredUrls.Count == 0)
        {
            throw BuildFetchException(PokemonIndexUrl, rosterHtml);
        }

        return discoveredUrls;
    }

    private static async Task<IReadOnlyList<string>> ResolveTargetUrlsWithBrowserAsync(
        IPage page,
        IReadOnlyList<string> targets,
        CancellationToken cancellationToken)
    {
        if (targets.Count > 0)
        {
            return targets.Select(ToGuideUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var rosterHtml = await GetUsableHtmlAsync(
            page,
            PokemonIndexUrl,
            async () =>
            {
                var guideUrls = await ExtractGuideUrlsFromPageAsync(page);
                if (guideUrls.Count > 0)
                {
                    return true;
                }

                var names = await ExtractPokemonNamesFromPageAsync(page);
                return names.Count > 0;
            },
            "Waiting for the UniteAPI Pokemon roster page to become usable. If Cloudflare is shown in Edge, complete it there; the updater will keep retrying automatically.",
            cancellationToken);

        var discoveredUrls = await ExtractGuideUrlsFromPageAsync(page);
        if (discoveredUrls.Count == 0)
        {
            var pokemonNames = await ExtractPokemonNamesFromPageAsync(page);
            discoveredUrls = pokemonNames
                .Select(ConvertPokemonNameToGuideUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (discoveredUrls.Count == 0)
        {
            throw BuildFetchException(PokemonIndexUrl, rosterHtml);
        }

        return discoveredUrls;
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

    private static string ToGuideUrl(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var slug = target.Trim().Trim('/').ToLowerInvariant();
        if (slug.StartsWith("best-builds-movesets-and-guide-for-", StringComparison.OrdinalIgnoreCase))
        {
            return UniteApiOrigin + "/pokemon/" + slug;
        }

        return UniteApiOrigin + GuidePathPrefix + slug;
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
                    EnsurePayloadContainsCounters(url, payload);
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

    private static async Task<string> GetUsableHtmlAsync(
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

        if (!IsLikelyCloudflareChallenge(html))
        {
            throw BuildFetchException(url, html);
        }

        if (!browserChallengePromptShown)
        {
            Console.WriteLine(prompt);
            browserChallengePromptShown = true;
        }

        // Keep polling automatically so one manual confirmation never blocks the rest of the run.
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

        throw BuildFetchException(url, html);
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
            var extractedJson = TryExtractPageJson(candidate);
            if (!string.IsNullOrWhiteSpace(extractedJson))
            {
                if (PayloadHasCounters(extractedJson))
                {
                    return extractedJson;
                }
            }

            if (LooksLikeJson(candidate) && PayloadHasCounters(candidate))
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

    private static async Task<IReadOnlyList<string>> ExtractGuideUrlsFromPageAsync(IPage page)
    {
        var hrefs = await page.EvaluateAsync<string[]>(
            @"() => Array.from(document.querySelectorAll('a[href]'))
                .map(anchor => anchor.href || anchor.getAttribute('href') || '')
                .filter(Boolean)");

        return hrefs
            .Where(href => href.Contains("/pokemon/best-builds-movesets-and-guide-for-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(href => href, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<string>> ExtractPokemonNamesFromPageAsync(IPage page)
    {
        var bodyText = await page.Locator("body").InnerTextAsync();
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return [];
        }

        var blockedLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Unite API",
            "Home",
            "Rankings",
            "Meta",
            "Pokemon",
            "Emblems",
            "About us",
            "Pokemon Unite Pokemons",
            "Pokémon Unite Pokemons",
            "Filters",
            "Melee",
            "Ranged",
            "Physical Attacker",
            "Special Attacker",
            "Attacker",
            "Defender",
            "Speedster",
            "Support",
            "All-Rounder"
        };

        return bodyText
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !blockedLines.Contains(line))
            .Where(line => !line.StartsWith("All assets, images and texts are owned by", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("We are not affiliated", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("This work is licensed", StringComparison.OrdinalIgnoreCase))
            .Where(IsLikelyPokemonName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsLikelyPokemonName(string value)
    {
        if (value.Length is < 3 or > 40)
        {
            return false;
        }

        return value.Any(char.IsLetter)
            && !value.Contains('%')
            && !char.IsDigit(value[0]);
    }

    private static string ConvertPokemonNameToGuideUrl(string pokemonName)
    {
        var normalized = pokemonName.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (character == '-')
            {
                builder.Append(character);
            }
        }

        return UniteApiOrigin + GuidePathPrefix + builder;
    }

    private static void WriteFailureDiagnostics(string url, string pageContent)
    {
        try
        {
            Directory.CreateDirectory(FailureDiagnosticsDirectory);
            var slug = Path.GetFileName(new Uri(url).AbsolutePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var outputPath = Path.Combine(FailureDiagnosticsDirectory, $"{timestamp}-{slug}.html");
            File.WriteAllText(outputPath, pageContent);
        }
        catch
        {
        }
    }

    private static void WriteResponseDiagnostics(string url, IReadOnlyList<BrowserResponseCapture> responses)
    {
        if (responses.Count == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(FailureDiagnosticsDirectory);
            var slug = Path.GetFileName(new Uri(url).AbsolutePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var outputPath = Path.Combine(FailureDiagnosticsDirectory, $"{timestamp}-{slug}-responses.txt");

            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            foreach (var response in responses)
            {
                writer.WriteLine($"URL: {response.Url}");
                writer.WriteLine($"Content-Type: {response.ContentType}");
                writer.WriteLine("Body:");
                var shouldTruncateBody =
                    response.Body.Length > 4000
                    && !response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase);

                writer.WriteLine(shouldTruncateBody ? response.Body[..4000] : response.Body);
                writer.WriteLine();
                writer.WriteLine(new string('-', 80));
            }
        }
        catch
        {
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

    private static void EnsurePayloadContainsCounters(string url, string pageJson)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(pageJson);
        if (PayloadHasCounters(doc.RootElement))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Downloaded payload for {url} but could not find matchup counters in the extracted payload.");
    }

    private static bool PayloadHasCounters(string pageJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(pageJson);
            return PayloadHasCounters(doc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    private static bool PayloadHasCounters(System.Text.Json.JsonElement root)
    {
        var blob = DecrypterService.FindPagePropsE(root);
        if (!string.IsNullOrWhiteSpace(blob))
        {
            try
            {
                var decryptedJson = DecrypterService.DecryptBlob(blob);
                using var decryptedDoc = System.Text.Json.JsonDocument.Parse(decryptedJson);
                return HasCountersNode(decryptedDoc.RootElement);
            }
            catch
            {
                return false;
            }
        }

        return HasCountersNode(root);
    }

    private static bool HasCountersNode(System.Text.Json.JsonElement root)
    {
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("counters", out var counters)
            && counters.ValueKind == System.Text.Json.JsonValueKind.Object
            && counters.TryGetProperty("all", out var allCounters)
            && allCounters.ValueKind == System.Text.Json.JsonValueKind.Array
            && allCounters.GetArrayLength() > 0)
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Object
                && HasCountersNode(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static Exception BuildFetchException(string url, string responseText)
    {
        var preview = responseText
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        if (preview.Length > 220)
        {
            preview = preview[..220] + "...";
        }

        var looksLikeRealGuidePage =
            responseText.Contains("guide: best builds and movesets", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("Statistics", StringComparison.OrdinalIgnoreCase);

        var hasCounterWords =
            responseText.Contains("Counters", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("\"counters\"", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("counter", StringComparison.OrdinalIgnoreCase);

        var cloudflareHint =
            IsLikelyCloudflareChallenge(responseText)
                ? " This looks like a Cloudflare challenge. Pass a real browser cookie header with --cookie-header, --cookie-file, or UNITE_DRAFTER_COOKIE_HEADER."
                : string.Empty;

        var countersHint = looksLikeRealGuidePage
            ? hasCounterWords
                ? " The page loaded, but I could not extract the counters payload format used by this Pokemon."
                : " The page loaded, but no counters payload was found for this Pokemon. UniteAPI may not expose matchup data for it yet."
            : string.Empty;

        return new InvalidOperationException(
            $"Could not extract a usable matchup payload from {url}. Response preview: {preview}{cloudflareHint}{countersHint}");
    }

    private static bool IsLikelyCloudflareChallenge(string responseText)
    {
        return responseText.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("Attention Required", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("/cdn-cgi/", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("challenge-platform", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeJson(string text)
    {
        return text.StartsWith('{') && text.EndsWith('}');
    }

    [GeneratedRegex("""(?<url>https://uniteapi\.dev/pokemon/best-builds-movesets-and-guide-for-[a-z0-9-]+|/pokemon/best-builds-movesets-and-guide-for-[a-z0-9-]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GuideUrlRegex();

    [GeneratedRegex("""<script[^>]*id=["']__NEXT_DATA__["'][^>]*>\s*(?<json>\{.*?\})\s*</script>""", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex NextDataScriptRegex();
}
