using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using DecrypterService = UniteDrafter.Decrypter.Decrypter;

namespace UniteDrafter.Tests.Decrypter;

public class DecrypterTests
{
    [Fact]
    public void FindPagePropsA_ReturnsDirectPagePropsValue()
    {
        const string json = """
            {
              "pageProps": {
                "a": "encrypted-value"
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);

        var result = DecrypterService.FindPagePropsA(doc.RootElement);

        Assert.Equal("encrypted-value", result);
    }

    [Fact]
    public void FindPagePropsA_ReturnsNestedPropsPagePropsValue()
    {
        const string json = """
            {
              "props": {
                "pageProps": {
                  "a": "nested-encrypted-value"
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);

        var result = DecrypterService.FindPagePropsA(doc.RootElement);

        Assert.Equal("nested-encrypted-value", result);
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
    public void DecryptBlob_FromRealFixture_ProducesParsableJson_And_WritesArtifactFile()
    {
        var fixturePath = ResolveFixturePath(
            "JsonsManually/rankings.json",
            "Notes/JsonExamples/rankings.json");

        var fixtureText = File.ReadAllText(fixturePath);
        using var encryptedDoc = JsonDocument.Parse(fixtureText);

        var blob = DecrypterService.FindPagePropsA(encryptedDoc.RootElement);
        Assert.False(string.IsNullOrWhiteSpace(blob));

        var decrypted = DecrypterService.DecryptBlob(blob!);
        using var decryptedDoc = JsonDocument.Parse(decrypted);

        Assert.Equal(JsonValueKind.Object, decryptedDoc.RootElement.ValueKind);

        var propertyCount = decryptedDoc.RootElement.EnumerateObject().Count();
        Assert.True(propertyCount > 0, "Expected decrypted payload object to contain at least one property.");

        var outputPath = WriteDecryptedArtifact(decryptedDoc.RootElement, "decrypted_rankings.json");
        Assert.True(File.Exists(outputPath), $"Expected artifact file to exist: {outputPath}");

        Console.WriteLine($"Decrypted artifact written to: {outputPath}");
    }


    [Fact]
    public void DecryptBlob_FromPlayerFixture_ProducesParsableJson_And_WritesArtifactFile()
    {
        var fixturePath = ResolveFixturePath(
            "JsonsManually/Players/DFM_Serata.json",
            "Notes/JsonExamples/RC_Häruto.json");

        var fixtureText = File.ReadAllText(fixturePath);
        using var encryptedDoc = JsonDocument.Parse(fixtureText);

        var blob = DecrypterService.FindPagePropsA(encryptedDoc.RootElement);
        Assert.False(string.IsNullOrWhiteSpace(blob));

        var decrypted = DecrypterService.DecryptBlob(blob!);
        using var decryptedDoc = JsonDocument.Parse(decrypted);

        Assert.Equal(JsonValueKind.Object, decryptedDoc.RootElement.ValueKind);
        Assert.True(decryptedDoc.RootElement.EnumerateObject().Any(), "Expected decrypted player payload to contain data.");

        var outputPath = WriteDecryptedArtifact(decryptedDoc.RootElement, "decrypted_player_DFM_Serata.json");
        Assert.True(File.Exists(outputPath), $"Expected artifact file to exist: {outputPath}");

        Console.WriteLine($"Player decrypted artifact written to: {outputPath}");
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
        var candidate1 = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Tests", "Decrypter", "TestResults"));
        if (Directory.Exists(Path.GetDirectoryName(candidate1) ?? string.Empty))
        {
            return candidate1;
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
