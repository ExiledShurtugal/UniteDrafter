using UniteDrafter.SourceUpdate.Data.Updating;
using Xunit;

namespace UniteDrafter.Tests.Decrypter;

public sealed class SourceUpdateTargetResolverTests
{
    [Fact]
    public void ExtractGuideUrls_ReturnsDistinctSortedGuideLinks()
    {
        const string html = """
            <a href="/pokemon/best-builds-movesets-and-guide-for-blastoise">Blastoise</a>
            <a href="https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-charizard">Charizard</a>
            <a href="/pokemon/best-builds-movesets-and-guide-for-blastoise">Duplicate</a>
            """;

        var urls = SourceUpdateTargetResolver.ExtractGuideUrls(html);

        Assert.Equal(2, urls.Count);
        Assert.Equal("https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-blastoise", urls[0]);
        Assert.Equal("https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-charizard", urls[1]);
    }
}
