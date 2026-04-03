using UniteDrafter.Data;

namespace UniteDrafter.Commands;

public static class SearchPokemonCommand
{
    private const string DatabasePath = "data/Database/unitedrafter.db";

    public static void Execute(string searchTerm)
    {
        var results = new PokemonDataReader(DatabasePath).SearchPokemon(searchTerm);
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
    }
}
