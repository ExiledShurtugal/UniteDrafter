using UniteDrafter.SourceUpdate.Data.Updating;
using UniteDrafter.Storage;
using Xunit;

namespace UniteDrafter.Tests.Decrypter;

public sealed class SourceUpdateOptionsParserTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "UniteDrafter.SourceUpdateOptionsTests", Guid.NewGuid().ToString("N"));
    private readonly string? originalStorageRoot =
        Environment.GetEnvironmentVariable(UniteDrafterStoragePaths.StorageRootEnvironmentVariableName);

    public SourceUpdateOptionsParserTests()
    {
        Environment.SetEnvironmentVariable(UniteDrafterStoragePaths.StorageRootEnvironmentVariableName, null);
    }

    [Fact]
    public void Parse_ReadsCookieFileAndBrowserFlags()
    {
        Directory.CreateDirectory(tempRoot);
        var cookieFile = Path.Combine(tempRoot, "cookie.txt");
        File.WriteAllText(cookieFile, "session=abc");
        var storageLayout = UniteDrafterStoragePaths.ResolveLayout(tempRoot);

        var options = SourceUpdateOptionsParser.Parse(
        [
            "--browser",
            "--headless",
            "--profile-dir", "profiles/edge",
            "--output-dir", "data/out",
            "--cookie-file", cookieFile,
            "blastoise",
            "charizard"
        ], tempRoot);

        Assert.True(options.UseBrowser);
        Assert.True(options.Headless);
        Assert.Equal(Path.Combine(storageLayout.RootPath, "profiles", "edge"), options.BrowserProfileDirectory);
        Assert.Equal(Path.Combine(storageLayout.RootPath, "data", "out"), options.OutputDirectory);
        Assert.Equal(
            Path.Combine(storageLayout.RootPath, "data", "Database", "Diagnostics", "SourceUpdateFailures"),
            options.DiagnosticsDirectory);
        Assert.Equal("session=abc", options.CookieHeader);
        Assert.Equal(["blastoise", "charizard"], options.Targets);
    }

    [Fact]
    public void Parse_FallsBackToEnvironmentCookieHeader()
    {
        const string envVarName = "UNITE_DRAFTER_COOKIE_HEADER";
        var original = Environment.GetEnvironmentVariable(envVarName);

        try
        {
            Environment.SetEnvironmentVariable(envVarName, "env-session=xyz");

            var options = SourceUpdateOptionsParser.Parse(["pikachu"], tempRoot);

            Assert.Equal("env-session=xyz", options.CookieHeader);
            Assert.Equal(["pikachu"], options.Targets);
            Assert.False(options.UseBrowser);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, original);
        }
    }

    [Fact]
    public void Parse_UsesSharedDefaultDirectories()
    {
        var options = SourceUpdateOptionsParser.Parse([], tempRoot);
        var layout = UniteDrafterStoragePaths.ResolveLayout(tempRoot);

        Assert.Equal(layout.GuideSourcesDirectory, options.OutputDirectory);
        Assert.Equal(layout.BrowserProfileDirectory, options.BrowserProfileDirectory);
        Assert.Equal(layout.SourceUpdateDiagnosticsDirectory, options.DiagnosticsDirectory);
        Assert.Empty(options.Targets);
        Assert.False(options.UseBrowser);
    }

    [Fact]
    public void Parse_ThrowsWhenOptionValueIsMissing()
    {
        var ex = Assert.Throws<ArgumentException>(() => SourceUpdateOptionsParser.Parse(["--output-dir"]));

        Assert.Contains("--output-dir", ex.Message);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            UniteDrafterStoragePaths.StorageRootEnvironmentVariableName,
            originalStorageRoot);
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
