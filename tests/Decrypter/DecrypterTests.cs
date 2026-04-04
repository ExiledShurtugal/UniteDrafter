using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using DecrypterService = UniteDrafter.SourceUpdate.Decrypter.Decrypter;
using BestBuildsReaderService = UniteDrafter.SourceUpdate.Decrypter.BestBuildsReader;
using UniteDrafter.SourceUpdate.Data.Updating;

namespace UniteDrafter.Tests.Decrypter;

public class DecrypterTests
{
    [Fact]
    public void FindPagePropsE_ReturnsDirectPagePropsValue()
    {
        const string json = """
            {
              "pageProps": {
                "e": "encrypted-value"
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);

        var result = DecrypterService.FindPagePropsE(doc.RootElement);

        Assert.Equal("encrypted-value", result);
    }

    [Fact]
    public void FindPagePropsE_ReturnsNestedPropsPagePropsValue()
    {
        const string json = """
            {
              "props": {
                "pageProps": {
                  "e": "nested-encrypted-value"
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);

        var result = DecrypterService.FindPagePropsE(doc.RootElement);

        Assert.Equal("nested-encrypted-value", result);
    }

    [Fact]
    public void FindPagePropsE_FallsBackToLegacyAValue()
    {
        const string json = """
            {
              "pageProps": {
                "a": "legacy-encrypted-value"
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);

        var result = DecrypterService.FindPagePropsE(doc.RootElement);

        Assert.Equal("legacy-encrypted-value", result);
    }

    [Fact]
    public void SplitBlobGuess_ExtractsEncPayloadAndKey()
    {
        var encPayload = Convert.ToBase64String(Enumerable.Range(1, 24).Select(i => (byte)i).ToArray());
        var key = "ABCDEFGHIJKLMNOPQRSTU"; // 21 chars
        var blob = encPayload + key;

        var (keyStr, encB64) = DecrypterService.SplitBlobGuess(blob);

        Assert.Equal(key, keyStr);
        Assert.Equal(encPayload, encB64);
    }

    [Fact]
    public void B64DecodeLoose_AcceptsUrlSafeBase64WithoutPadding()
    {
        var source = Encoding.UTF8.GetBytes("hello-world");
        var standard = Convert.ToBase64String(source);
        var urlSafeWithoutPadding = standard.TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var decoded = DecrypterService.B64DecodeLoose(urlSafeWithoutPadding);

        Assert.Equal(source, decoded);
    }


    [Fact]
    public void DecryptBlob_RoundTripsKnownPlaintext()
    {
        const string plaintext = "test payload for ctr mode";
        const string keyStr = "ABCDEFGHIJKLMNOPQRSTU"; // 21 chars

        var key = SHA256.HashData(Encoding.UTF8.GetBytes(keyStr));
        var iv = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var ciphertext = EncryptCtr(Encoding.UTF8.GetBytes(plaintext), key, iv);

        var raw = iv.Concat(ciphertext).ToArray();
        var encB64 = Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var blob = encB64 + keyStr;

        var decrypted = DecrypterService.DecryptBlob(blob);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void FindPagePropsE_FromBlastoiseFixture_ReturnsValue()
    {
        var fixturePath = ResolveFixturePath(
            "notes/JsonExamples/best-builds-movesets-and-guide-for-blastoise.json");

        var fixtureText = File.ReadAllText(fixturePath);
        using var encryptedDoc = JsonDocument.Parse(fixtureText);

        var blob = DecrypterService.FindPagePropsE(encryptedDoc.RootElement);

        Assert.False(string.IsNullOrWhiteSpace(blob));
    }

    [Fact]
    public void DecryptBlob_FromBlastoiseFixture_ProducesParsableJson_And_WritesArtifactFile()
    {
        var fixturePath = ResolveFixturePath(
            "notes/JsonExamples/best-builds-movesets-and-guide-for-blastoise.json");

        var fixtureText = File.ReadAllText(fixturePath);
        using var encryptedDoc = JsonDocument.Parse(fixtureText);

        var blob = DecrypterService.FindPagePropsE(encryptedDoc.RootElement);
        Assert.False(string.IsNullOrWhiteSpace(blob));

        var decrypted = DecrypterService.DecryptBlob(blob!);
        using var decryptedDoc = JsonDocument.Parse(decrypted);

        Assert.Equal(JsonValueKind.Object, decryptedDoc.RootElement.ValueKind);
        Assert.True(decryptedDoc.RootElement.EnumerateObject().Any(), "Expected decrypted Blastoise payload to contain data.");

        var outputPath = WriteDecryptedArtifact(decryptedDoc.RootElement, "decrypted_blastoise.json");
        Assert.True(File.Exists(outputPath), $"Expected artifact file to exist: {outputPath}");

        Console.WriteLine($"Blastoise decrypted artifact written to: {outputPath}");
    }

    [Fact]
    public void BestBuildsReader_FromEncryptedFixture_ParsesPokemonWinRates()
    {
        var fixturePath = ResolveFixturePath(
            "notes/JsonExamples/best-builds-movesets-and-guide-for-blastoise.json");

        var data = BestBuildsReaderService.ReadPokemonWinRatesFromEncryptedPageFile(fixturePath);

        Assert.Equal(180007, data.Pokemon.UniteApiId);
        Assert.Equal(7, data.Pokemon.PokedexId);
        Assert.Equal("Blastoise", data.Pokemon.PokemonName);
        Assert.NotEmpty(data.Pokemon.PokemonImg);
        Assert.True(data.CounterSections.ContainsKey("all"));
        Assert.NotEmpty(data.CounterSections["all"]);

        var topMatchup = data.CounterSections["all"][0];
        Assert.True(topMatchup.OpponentUniteApiId > 0);
        Assert.False(string.IsNullOrWhiteSpace(topMatchup.OpponentPokemonName));
        Assert.True(topMatchup.WinRate > 0);
    }

    [Fact]
    public void ParsePokemonWinRatesFromDecryptedPayload_ThrowsWhenCountersPokemonIdIsMissing()
    {
        const string json = """
            {
              "pokemon": {
                "id": 7,
                "name": {
                  "en": "Blastoise"
                },
                "icons": {
                  "square": "blastoise.png"
                }
              },
              "counters": {
                "all": [
                  {
                    "pokemonId": 180006,
                    "name": "Charizard",
                    "img": "charizard.png",
                    "winRate": 52.5
                  }
                ]
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BestBuildsReaderService.ParsePokemonWinRatesFromDecryptedPayload(doc.RootElement));

        Assert.Contains("counters.pokemonId", ex.Message);
    }

    [Fact]
    public void TryExtractPageJson_ReturnsRawJsonWhenResponseAlreadyIsJson()
    {
        const string responseText = """{"pageProps":{"e":"blob"}}""";

        var extracted = UniteApiSourceUpdater.TryExtractPageJson(responseText);

        Assert.Equal(responseText, extracted);
    }

    [Fact]
    public void TryExtractPageJson_ExtractsNextDataScriptFromHtml()
    {
        const string html = """
            <html>
              <body>
                <script id="__NEXT_DATA__" type="application/json">
                  {"pageProps":{"e":"blob"}}
                </script>
              </body>
            </html>
            """;

        var extracted = UniteApiSourceUpdater.TryExtractPageJson(html);

        Assert.Equal("""{"pageProps":{"e":"blob"}}""", extracted);
    }

    [Fact]
    public void ExtractGuideUrls_ReturnsDistinctGuideLinks()
    {
        const string html = """
            <a href="/pokemon/best-builds-movesets-and-guide-for-blastoise">Blastoise</a>
            <a href="https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-alolanraichu">Alolan Raichu</a>
            <a href="/pokemon/best-builds-movesets-and-guide-for-blastoise">Blastoise again</a>
            """;

        var urls = UniteApiSourceUpdater.ExtractGuideUrls(html);

        Assert.Equal(2, urls.Count);
        Assert.Contains("https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-blastoise", urls);
        Assert.Contains("https://uniteapi.dev/pokemon/best-builds-movesets-and-guide-for-alolanraichu", urls);
    }


    private static string ResolveFixturePath(params string[] relativePaths)
    {
        var roots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var root in roots)
        {
            var dir = new DirectoryInfo(root);
            while (dir is not null)
            {
                foreach (var relativePath in relativePaths)
                {
                    var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                    var candidate = Path.Combine(dir.FullName, normalized);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate any fixture file. Tried: {string.Join(", ", relativePaths)}");
    }

    private static string WriteDecryptedArtifact(JsonElement rootElement, string outputFileName)
    {
        var outputDir = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, outputFileName);
        var pretty = JsonSerializer.Serialize(rootElement, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(outputPath, pretty);
        return outputPath;
    }

    private static string ResolveOutputDirectory()
    {
        var roots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var root in roots)
        {
            var dir = new DirectoryInfo(root);
            while (dir is not null)
            {
                var lowerCaseCandidate = Path.Combine(dir.FullName, "tests", "Decrypter");
                if (Directory.Exists(lowerCaseCandidate))
                {
                    return Path.Combine(lowerCaseCandidate, "TestResults");
                }

                dir = dir.Parent;
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "TestResults"));
    }

    private static byte[] EncryptCtr(byte[] plaintext, byte[] key, byte[] iv)
    {
        var output = new byte[plaintext.Length];
        var counterBlock = (byte[])iv.Clone();

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();

        var blockCount = (plaintext.Length + 15) / 16;
        for (var i = 0; i < blockCount; i++)
        {
            var keystreamBlock = new byte[16];
            encryptor.TransformBlock(counterBlock, 0, 16, keystreamBlock, 0);

            for (var j = 0; j < 16 && (i * 16 + j) < plaintext.Length; j++)
            {
                output[i * 16 + j] = (byte)(plaintext[i * 16 + j] ^ keystreamBlock[j]);
            }

            IncrementCounter(counterBlock);
        }

        return output;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (var i = 15; i >= 0; i--)
        {
            if (++counter[i] != 0)
            {
                break;
            }
        }
    }
}

