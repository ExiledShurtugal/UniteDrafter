using UniteDrafter.Services;
using UniteDrafter.Storage;

namespace UniteDrafter.Commands;

public static class SearchPokemonCommand
{
    public static void Execute(string searchTerm)
    {
        DatabaseBootstrapper.EnsureInitialized();
        var dataSource = DraftPageDataSourceFactory.CreateForWorkingDirectory(AppContext.BaseDirectory);
        var availabilityError = dataSource.GetAvailabilityError();
        if (!string.IsNullOrWhiteSpace(availabilityError))
        {
            Console.WriteLine(availabilityError);
            return;
        }

        var results = dataSource.SearchPokemon(searchTerm);
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
