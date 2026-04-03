using UniteDrafter.Data;

namespace UniteDrafter.Commands;

public static class MatchupsCommand
{
    private const string DatabasePath = "data/Database/unitedrafter.db";

    public static void Execute(string pokemonName)
    {
        var matchupReader = new PokemonMatchupDataReader(DatabasePath);
        var pokemonReader = new PokemonDataReader(DatabasePath);
        var matchups = matchupReader.GetMatchupsForPokemon(pokemonName);

        if (matchups.Count == 0)
        {
            Console.WriteLine($"No matchups found for pokemon: {pokemonName}");
            var matches = pokemonReader.SearchPokemon(pokemonName, limit: 5);
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
