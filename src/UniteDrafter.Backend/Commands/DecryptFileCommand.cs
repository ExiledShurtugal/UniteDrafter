using System.Text.Json;
using BestBuildsDecrypter = UniteDrafter.SourceUpdate.Decrypter.Decrypter;

namespace UniteDrafter.Commands;

public static class DecryptFileCommand
{
    public static void Execute(string inputPath, string outputPath)
    {
        var pageText = File.ReadAllText(inputPath);
        using var pageDoc = JsonDocument.Parse(pageText);
        var blob = BestBuildsDecrypter.FindPagePropsE(pageDoc.RootElement)
            ?? throw new InvalidOperationException("pageProps.e/pageProps.a not found");
        var decrypted = BestBuildsDecrypter.DecryptBlob(blob);
        using var decryptedDoc = JsonDocument.Parse(decrypted);

        var pretty = JsonSerializer.Serialize(decryptedDoc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, pretty);
        Console.WriteLine($"Wrote decrypted json to: {outputPath}");
    }
}
