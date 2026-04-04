using UniteDrafter.SourceUpdate.Data.Updating;
using Xunit;

namespace UniteDrafter.Tests.Decrypter;

public sealed class SourceUpdatePayloadInspectorTests
{
    [Fact]
    public void TryExtractPageJson_ReturnsEmbeddedNextDataJson()
    {
        const string html = """
            <html>
              <body>
                <script id="__NEXT_DATA__" type="application/json">
                  {"props":{"pageProps":{"pokemon":{"name":{"en":"Blastoise"}}}}}
                </script>
              </body>
            </html>
            """;

        var result = SourceUpdatePayloadInspector.TryExtractPageJson(html);

        Assert.NotNull(result);
        Assert.Contains("Blastoise", result);
    }

    [Fact]
    public void PayloadHasCounters_ReturnsTrue_WhenCountersExist()
    {
        const string json = """
            {
              "counters": {
                "all": [
                  { "name": "Charizard", "winRate": 52.5 }
                ]
              }
            }
            """;

        Assert.True(SourceUpdatePayloadInspector.PayloadHasCounters(json));
    }
}
