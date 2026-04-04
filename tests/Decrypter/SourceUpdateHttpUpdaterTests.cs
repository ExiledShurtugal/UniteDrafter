using System.Net;
using System.Net.Http;
using UniteDrafter.SourceUpdate.Data.Updating;
using Xunit;

namespace UniteDrafter.Tests.Decrypter;

public sealed class SourceUpdateHttpUpdaterTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "UniteDrafter.SourceUpdateHttpUpdaterTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UpdateAsync_WithExplicitTarget_SavesPayloadAndReturnsSuccessSummary()
    {
        var outputDirectory = Path.Combine(tempRoot, "output");
        const string url = "https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-blastoise";
        const string payload = """{"counters":{"all":[{"slug":"pikachu"}]}}""";
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(url, request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            };
        }));

        var summary = await SourceUpdateHttpUpdater.UpdateAsync(
            CreateOptions(outputDirectory, [url]),
            httpClient,
            CancellationToken.None);

        Assert.Equal(1, summary.SavedFiles);
        Assert.Equal(0, summary.FailedFiles);
        Assert.Equal([url], summary.DiscoveredUrls);
        Assert.Empty(summary.Failures);
        Assert.Equal(
            payload,
            File.ReadAllText(Path.Combine(outputDirectory, "best-builds-movesets-and-guide-for-blastoise.json")));
    }

    [Fact]
    public async Task UpdateAsync_WithoutExplicitTargets_ResolvesRosterAndSavesDiscoveredGuide()
    {
        var outputDirectory = Path.Combine(tempRoot, "output");
        const string rosterUrl = "https://uniteapi.dev/pokemon";
        const string guideUrl = "https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-pikachu";
        const string rosterHtml = """
            <html>
              <body>
                <a href="/pokemon/best-builds-movesets-and-guide-for-pikachu">Pikachu</a>
              </body>
            </html>
            """;
        const string payload = """{"counters":{"all":[{"slug":"blastoise"}]}}""";
        var requestedUrls = new List<string>();

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var requestUrl = request.RequestUri?.ToString() ?? string.Empty;
            requestedUrls.Add(requestUrl);

            return requestUrl switch
            {
                rosterUrl => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(rosterHtml)
                },
                guideUrl => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload)
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        }));

        var summary = await SourceUpdateHttpUpdater.UpdateAsync(
            CreateOptions(outputDirectory, []),
            httpClient,
            CancellationToken.None);

        Assert.Equal([rosterUrl, guideUrl], requestedUrls);
        Assert.Equal([guideUrl], summary.DiscoveredUrls);
        Assert.Equal(1, summary.SavedFiles);
        Assert.Equal(0, summary.FailedFiles);
        Assert.Empty(summary.Failures);
        Assert.True(File.Exists(Path.Combine(outputDirectory, "best-builds-movesets-and-guide-for-pikachu.json")));
    }

    private static SourceUpdateOptions CreateOptions(string outputDirectory, IReadOnlyList<string> targets) =>
        new(
            outputDirectory,
            targets,
            CookieHeader: null,
            UseBrowser: false,
            Headless: true,
            BrowserProfileDirectory: Path.Combine(outputDirectory, "browser-profile"),
            DiagnosticsDirectory: Path.Combine(outputDirectory, "diagnostics"));

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
