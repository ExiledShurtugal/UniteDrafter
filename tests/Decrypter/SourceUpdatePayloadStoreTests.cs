using UniteDrafter.SourceUpdate.Data.Updating;
using Xunit;

namespace UniteDrafter.Tests.Decrypter;

public sealed class SourceUpdatePayloadStoreTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(
        Path.GetTempPath(),
        "UniteDrafter.SourceUpdatePayloadStoreTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SavePayloadAsync_WritesJsonUsingGuideSlugAsFileName()
    {
        var outputDirectory = Path.Combine(tempRoot, "output");
        const string url = "https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-blastoise";
        const string payload = """{"pokemon":{"name":{"en":"Blastoise"}}}""";

        var outputPath = await SourceUpdatePayloadStore.SavePayloadAsync(
            outputDirectory,
            url,
            payload);

        Assert.Equal(
            Path.Combine(outputDirectory, "best-builds-movesets-and-guide-for-blastoise.json"),
            outputPath);
        Assert.True(File.Exists(outputPath));
        Assert.Equal(payload, File.ReadAllText(outputPath));
    }

    [Fact]
    public void GetOutputFileName_ReturnsGuideSlugJsonName()
    {
        const string url = "https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-pikachu";

        var outputFileName = SourceUpdatePayloadStore.GetOutputFileName(url);

        Assert.Equal("best-builds-movesets-and-guide-for-pikachu.json", outputFileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
