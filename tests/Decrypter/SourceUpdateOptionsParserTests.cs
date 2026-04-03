using UniteDrafter.Data.Updating;
using Xunit;

namespace UniteDrafter.Tests.Decrypter;

public sealed class SourceUpdateOptionsParserTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "UniteDrafter.SourceUpdateOptionsTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Parse_ReadsCookieFileAndBrowserFlags()
    {
        Directory.CreateDirectory(tempRoot);
        var cookieFile = Path.Combine(tempRoot, "cookie.txt");
        File.WriteAllText(cookieFile, "session=abc");

        var options = SourceUpdateOptionsParser.Parse(
        [
            "--browser",
            "--headless",
            "--profile-dir", "profiles/edge",
            "--output-dir", "data/out",
            "--cookie-file", cookieFile,
            "blastoise",
            "charizard"
        ]);

        Assert.True(options.UseBrowser);
        Assert.True(options.Headless);
        Assert.Equal("profiles/edge", options.BrowserProfileDirectory);
        Assert.Equal("data/out", options.OutputDirectory);
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

            var options = SourceUpdateOptionsParser.Parse(["pikachu"]);

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
    public void Parse_ThrowsWhenOptionValueIsMissing()
    {
        var ex = Assert.Throws<ArgumentException>(() => SourceUpdateOptionsParser.Parse(["--output-dir"]));

        Assert.Contains("--output-dir", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
