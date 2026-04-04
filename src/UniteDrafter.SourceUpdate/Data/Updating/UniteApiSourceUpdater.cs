using Microsoft.Playwright;

namespace UniteDrafter.SourceUpdate.Data.Updating;

public sealed record SourceUpdateOptions(
    string OutputDirectory,
    IReadOnlyList<string> Targets,
    string? CookieHeader,
    bool UseBrowser,
    bool Headless,
    string BrowserProfileDirectory,
    string DiagnosticsDirectory);

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

    public static async Task<SourceUpdateSummary> UpdateAsync(
        SourceUpdateOptions options,
        ISourceUpdateReporter? reporter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        reporter ??= NullSourceUpdateReporter.Instance;

        if (options.UseBrowser)
        {
            return await UpdateWithBrowserAsync(options, reporter, cancellationToken);
        }

        return await SourceUpdateHttpUpdater.UpdateAsync(options, cancellationToken);
    }

    private static async Task<SourceUpdateSummary> UpdateWithBrowserAsync(
        SourceUpdateOptions options,
        ISourceUpdateReporter reporter,
        CancellationToken cancellationToken)
    {
        var failures = new List<SourceUpdateFailure>();
        var savedFiles = 0;

        await using var browserSession = await SourceUpdateBrowserSession.CreateAsync(options);
        var page = browserSession.Page;
        var recentResponseBodies = browserSession.RecentResponseBodies;

        reporter.ReportBrowserModeStarted(options.BrowserProfileDirectory);
        browserChallengePromptShown = false;

        var urls = await SourceUpdateTargetResolver.ResolveTargetUrlsWithBrowserAsync(
            page,
            options.Targets,
            (browserPage, url, isUsable, prompt, ct) => GetUsableHtmlAsync(browserPage, url, isUsable, prompt, reporter, ct),
            cancellationToken);
        foreach (var url in urls)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var responseText = await SourceUpdateBrowserPayloadExtractor.GetPagePayloadAsync(
                    page,
                    url,
                    recentResponseBodies,
                    GetUsableHtmlAsync,
                    reporter,
                    cancellationToken);
                var extractedJson = SourceUpdatePayloadInspector.TryExtractPageJson(responseText);
                if (string.IsNullOrWhiteSpace(extractedJson))
                {
                    throw SourceUpdatePayloadInspector.BuildFetchException(url, responseText);
                }

                SourceUpdatePayloadInspector.EnsurePayloadContainsCounters(url, extractedJson);
                await SourceUpdatePayloadStore.SavePayloadAsync(
                    options.OutputDirectory,
                    url,
                    extractedJson,
                    cancellationToken);
                savedFiles++;

                reporter.ReportSavedPayload(SourceUpdatePayloadStore.GetOutputFileName(url));
            }
            catch (Exception ex)
            {
                await SourceUpdateFailureRecorder.RecordBrowserFailureAsync(
                    options.DiagnosticsDirectory,
                    url,
                    page,
                    recentResponseBodies.ToArray());
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
        ISourceUpdateReporter reporter,
        CancellationToken cancellationToken)
    {
        await SourceUpdateBrowserPayloadExtractor.NavigateAsync(page, url);
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
            reporter.ReportChallengePrompt(prompt);
            browserChallengePromptShown = true;
        }

        for (var attempt = 0; attempt < 15; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            await SourceUpdateBrowserPayloadExtractor.NavigateAsync(page, url);
            html = await page.ContentAsync();
            if (await isUsable())
            {
                return html;
            }
        }

        throw SourceUpdatePayloadInspector.BuildFetchException(url, html);
    }
}
