using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
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

        if (args.Length == 2 && string.Equals(args[0], "matchups", StringComparison.OrdinalIgnoreCase))
        {
            var pokemonName = args[1];

            using var connection = new SqliteConnection("Data Source=data/Database/unitedrafter.db");
            connection.Open();

            var matchups = DatabaseQueries.GetMatchupsForPokemon(connection, pokemonName);
            if (matchups.Count == 0)
            {
                Console.WriteLine($"No matchups found for pokemon: {pokemonName}");
                var matches = DatabaseQueries.SearchPokemon(connection, pokemonName, limit: 5);
                if (matches.Count > 0)
                {
                    Console.WriteLine("Closest pokemon matches:");
                    foreach (var match in matches)
                    {
                        Console.WriteLine($"- {match.PokemonName}");
                    }
                }
                return;
            }

            Console.WriteLine($"Matchups for {matchups[0].PokemonName}:");
            foreach (var matchup in matchups)
            {
                Console.WriteLine($"{matchup.OpponentName}: {matchup.WinRate:F1}%");
            }
            return;
        }

        if (args.Length == 2 && string.Equals(args[0], "search-pokemon", StringComparison.OrdinalIgnoreCase))
        {
            var searchTerm = args[1];

            using var connection = new SqliteConnection("Data Source=data/Database/unitedrafter.db");
            connection.Open();

            var results = DatabaseQueries.SearchPokemon(connection, searchTerm);
            if (results.Count == 0)
            {
                Console.WriteLine($"No pokemon found for search term: {searchTerm}");
                return;
            }

            Console.WriteLine($"Pokemon matches for \"{searchTerm}\":");
            foreach (var result in results)
            {
                var pokedexSuffix = result.PokedexId.HasValue ? $" (Pokedex #{result.PokedexId.Value})" : string.Empty;
                Console.WriteLine($"- {result.PokemonName}{pokedexSuffix}");
            }
            return;
        }

        DatabaseInitializer.Initialize();
    }
}
