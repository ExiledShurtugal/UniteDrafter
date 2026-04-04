using System.Net;

namespace UniteDrafter.SourceUpdate.Data.Updating;

internal static class SourceUpdateHttpUpdater
{
    private const string UniteApiOrigin = "https://uniteapi.dev";

    public static async Task<SourceUpdateSummary> UpdateAsync(
        SourceUpdateOptions options,
        CancellationToken cancellationToken)
    {
        using var httpClient = CreateClient(options.CookieHeader);
        return await UpdateAsync(options, httpClient, cancellationToken);
    }

    internal static async Task<SourceUpdateSummary> UpdateAsync(
        SourceUpdateOptions options,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
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
                await SourceUpdatePayloadStore.SavePayloadAsync(
                    options.OutputDirectory,
                    url,
                    extractedJson,
                    cancellationToken);
                savedFiles++;
            }
            catch (Exception ex)
            {
                SourceUpdateFailureRecorder.RecordHttpFailure(
                    options.DiagnosticsDirectory,
                    url,
                    responseText ?? string.Empty);
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
}
