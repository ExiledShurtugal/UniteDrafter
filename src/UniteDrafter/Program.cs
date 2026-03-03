using System;
using System.IO;
using System.Text.Json;
using UniteDrafter.Data;
using UniteDrafter.Decrypter;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 3 && string.Equals(args[0], "decrypt-file", StringComparison.OrdinalIgnoreCase))
        {
            var inputPath = args[1];
            var outputPath = args[2];

            var pageText = File.ReadAllText(inputPath);
            using var pageDoc = JsonDocument.Parse(pageText);
            var blob = Decrypter.FindPagePropsE(pageDoc.RootElement) ?? throw new InvalidOperationException("pageProps.e/pageProps.a not found");
            var decrypted = Decrypter.DecryptBlob(blob);
            using var decryptedDoc = JsonDocument.Parse(decrypted);

            var pretty = JsonSerializer.Serialize(decryptedDoc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            File.WriteAllText(outputPath, pretty);
            Console.WriteLine($"Wrote decrypted json to: {outputPath}");
            return;
        }

        if (args.Length == 2 && string.Equals(args[0], "decrypt-ids", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = args[1];

            var parsed = BestBuildsReader.ReadPokemonWinRatesFromEncryptedPageFile(filePath);
            Console.WriteLine($"pokemon.name.en={parsed.Pokemon.PokemonName}");
            Console.WriteLine($"pokemon.id={parsed.Pokemon.PokedexId}");
            Console.WriteLine($"counters.pokemonId={parsed.Pokemon.UniteApiId}");

            var pageText = File.ReadAllText(filePath);
            using var pageDoc = JsonDocument.Parse(pageText);
            var blob = Decrypter.FindPagePropsE(pageDoc.RootElement) ?? throw new InvalidOperationException("pageProps.e/pageProps.a not found");
            var decrypted = Decrypter.DecryptBlob(blob);
            using var decryptedDoc = JsonDocument.Parse(decrypted);
            var countersPokemonId = decryptedDoc.RootElement.GetProperty("counters").GetProperty("pokemonId").GetInt32();
            Console.WriteLine($"counters.pokemonId(raw)={countersPokemonId}");
            return;
        }

        DatabaseInitializer.Initialize();
    }
}
