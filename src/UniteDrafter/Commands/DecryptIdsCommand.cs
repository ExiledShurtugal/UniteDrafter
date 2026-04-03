using System.Text.Json;
using UniteDrafter.Decrypter;
using BestBuildsDecrypter = UniteDrafter.Decrypter.Decrypter;

namespace UniteDrafter.Commands;

public static class DecryptIdsCommand
{
    public static void Execute(string filePath)
    {
        var parsed = BestBuildsReader.ReadPokemonWinRatesFromEncryptedPageFile(filePath);
        Console.WriteLine($"pokemon.name.en={parsed.Pokemon.PokemonName}");
        Console.WriteLine($"pokemon.id={parsed.Pokemon.PokedexId}");
        Console.WriteLine($"counters.pokemonId={parsed.Pokemon.UniteApiId}");

        var pageText = File.ReadAllText(filePath);
        using var pageDoc = JsonDocument.Parse(pageText);
        var blob = BestBuildsDecrypter.FindPagePropsE(pageDoc.RootElement)
            ?? throw new InvalidOperationException("pageProps.e/pageProps.a not found");
        var decrypted = BestBuildsDecrypter.DecryptBlob(blob);
        using var decryptedDoc = JsonDocument.Parse(decrypted);
        var countersPokemonId = decryptedDoc.RootElement.GetProperty("counters").GetProperty("pokemonId").GetInt32();
        Console.WriteLine($"counters.pokemonId(raw)={countersPokemonId}");
    }
}
