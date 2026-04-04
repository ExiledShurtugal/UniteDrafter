using UniteDrafter.Services;
using UniteDrafter.Storage;

namespace UniteDrafter.Commands;

public static class MatchupsCommand
{
    public static void Execute(string pokemonName)
    {
        DatabaseBootstrapper.EnsureInitialized();
        var dataSource = DraftPageDataSourceFactory.CreateForWorkingDirectory(AppContext.BaseDirectory);
        var availabilityError = dataSource.GetAvailabilityError();
        if (!string.IsNullOrWhiteSpace(availabilityError))
        {
            Console.WriteLine(availabilityError);
            return;
        }

        var matchups = dataSource.GetMatchupsForPokemon(pokemonName);

        if (matchups.Count == 0)
        {
            Console.WriteLine($"No matchups found for pokemon: {pokemonName}");
            var matches = dataSource.SearchPokemon(pokemonName, limit: 5);
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
    }
}
