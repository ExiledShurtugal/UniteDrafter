using UniteDrafter.SourceUpdate.Data;
using UniteDrafter.SourceUpdate.Data.Updating;
using Xunit;

namespace UniteDrafter.Tests.Decrypter;

public sealed class DatabaseRefreshWorkflowTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "UniteDrafter.DatabaseRefreshWorkflowTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RefreshAndRebuildAsync_PromotesSuccessfulSnapshotAndRebuildsFromPromotedDirectory()
    {
        var outputDirectory = Path.Combine(tempRoot, "GuideSources");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "old.json"), "old-payload");

        string? rebuiltDirectory = null;
        string? rebuiltPayload = null;
        const string url = "https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-blastoise";

        var result = await DatabaseRefreshWorkflow.RefreshAndRebuildAsync(
            CreateOptions(outputDirectory),
            reporter: null,
            async (stagingOptions, _, _) =>
            {
                await File.WriteAllTextAsync(
                    Path.Combine(stagingOptions.OutputDirectory, "best-builds-movesets-and-guide-for-blastoise.json"),
                    "new-payload");

                return new SourceUpdateSummary(
                    stagingOptions.OutputDirectory,
                    SavedFiles: 1,
                    FailedFiles: 0,
                    DiscoveredUrls: [url],
                    Failures: []);
            },
            directory =>
            {
                rebuiltDirectory = directory;
                rebuiltPayload = File.ReadAllText(
                    Path.Combine(directory, "best-builds-movesets-and-guide-for-blastoise.json"));
            },
            CancellationToken.None);

        Assert.True(result.PromotedSnapshot);
        Assert.True(result.RebuiltDatabase);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(Path.GetFullPath(outputDirectory), Path.GetFullPath(result.SourceDirectory));
        Assert.Equal(Path.GetFullPath(outputDirectory), Path.GetFullPath(rebuiltDirectory!));
        Assert.Equal(
            "new-payload",
            File.ReadAllText(Path.Combine(outputDirectory, "best-builds-movesets-and-guide-for-blastoise.json")));
        Assert.Equal("new-payload", rebuiltPayload);
        Assert.False(File.Exists(Path.Combine(outputDirectory, "old.json")));
        Assert.Empty(EnumerateStagingDirectories(outputDirectory));
    }

    [Fact]
    public async Task RefreshAndRebuildAsync_KeepsExistingSnapshotAndSkipsRebuildWhenUpdateFails()
    {
        var outputDirectory = Path.Combine(tempRoot, "GuideSources");
        Directory.CreateDirectory(outputDirectory);
        var livePayloadPath = Path.Combine(outputDirectory, "best-builds-movesets-and-guide-for-blastoise.json");
        File.WriteAllText(livePayloadPath, "old-payload");

        var rebuildCalled = false;
        const string url = "https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-charizard";

        var result = await DatabaseRefreshWorkflow.RefreshAndRebuildAsync(
            CreateOptions(outputDirectory),
            reporter: null,
            async (stagingOptions, _, _) =>
            {
                await File.WriteAllTextAsync(
                    Path.Combine(stagingOptions.OutputDirectory, "best-builds-movesets-and-guide-for-charizard.json"),
                    "partial-payload");

                return new SourceUpdateSummary(
                    stagingOptions.OutputDirectory,
                    SavedFiles: 1,
                    FailedFiles: 1,
                    DiscoveredUrls: [url],
                    Failures:
                    [
                        new SourceUpdateFailure(url, "Cloudflare challenge")
                    ]);
            },
            _ => rebuildCalled = true,
            CancellationToken.None);

        Assert.False(result.PromotedSnapshot);
        Assert.False(result.RebuiltDatabase);
        Assert.False(rebuildCalled);
        Assert.Contains("left unchanged", result.ErrorMessage);
        Assert.Equal("old-payload", File.ReadAllText(livePayloadPath));
        Assert.False(File.Exists(Path.Combine(outputDirectory, "best-builds-movesets-and-guide-for-charizard.json")));
        Assert.Empty(EnumerateStagingDirectories(outputDirectory));
    }

    private static SourceUpdateOptions CreateOptions(string outputDirectory) =>
        new(
            outputDirectory,
            Targets: [],
            CookieHeader: null,
            UseBrowser: false,
            Headless: true,
            BrowserProfileDirectory: Path.Combine(outputDirectory, "browser-profile"),
            DiagnosticsDirectory: Path.Combine(outputDirectory, "diagnostics"));

    private static IReadOnlyList<string> EnumerateStagingDirectories(string outputDirectory)
    {
        var parentDirectory = Path.GetDirectoryName(Path.GetFullPath(outputDirectory))!;
        var outputDirectoryName = Path.GetFileName(Path.GetFullPath(outputDirectory));
        return Directory.EnumerateDirectories(parentDirectory, $".{outputDirectoryName}.staging-*").ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
