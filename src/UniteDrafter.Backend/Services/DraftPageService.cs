using UniteDrafter.Data;

namespace UniteDrafter.Services;

public sealed class DraftPageService : IDraftPageService
{
    private readonly string databasePath;
    private readonly IPokemonDataReader pokemonDataReader;
    private readonly IPokemonMatchupDataReader pokemonMatchupDataReader;

    public DraftPageService(
        string databasePath,
        IPokemonDataReader pokemonDataReader,
        IPokemonMatchupDataReader pokemonMatchupDataReader)
    {
        this.databasePath = Path.GetFullPath(databasePath);
        this.pokemonDataReader = pokemonDataReader;
        this.pokemonMatchupDataReader = pokemonMatchupDataReader;
    }

    public PokemonSearchResponse SearchPokemon(string searchTerm, int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new PokemonSearchResponse([], null);
        }

        if (!File.Exists(databasePath))
        {
            return new PokemonSearchResponse([], $"Database file not found at: {databasePath}");
        }

        try
        {
            var results = pokemonDataReader.SearchPokemon(searchTerm, limit);
            return new PokemonSearchResponse(results, null);
        }
        catch (Exception ex)
        {
            return new PokemonSearchResponse([], $"Database search failed: {GetExceptionMessage(ex)}");
        }
    }

    public PokemonDraftDetailsResponse GetPokemonDraftDetails(string pokemonName, int matchupLimit = 5)
    {
        if (string.IsNullOrWhiteSpace(pokemonName) || !File.Exists(databasePath))
        {
            return new PokemonDraftDetailsResponse(null, $"Database file not found at: {databasePath}");
        }

        try
        {
            var profile = pokemonDataReader.GetPokemonProfile(pokemonName);
            if (profile is null)
            {
                return new PokemonDraftDetailsResponse(null, $"Could not find Pokemon details for \"{pokemonName}\".");
            }

            var matchups = pokemonMatchupDataReader.GetMatchupsForPokemon(profile.PokemonName);
            var bestAgainst = matchups.Take(matchupLimit).ToArray();
            var worstAgainst = matchups.TakeLast(Math.Min(matchupLimit, matchups.Count)).Reverse().ToArray();
            var counterStatusMessage = matchups.Count == 0
                ? "No counters available."
                : null;

            return new PokemonDraftDetailsResponse(
                new PokemonDraftDetails(
                    profile.UniteApiId,
                    profile.PokedexId,
                    profile.PokemonName,
                    profile.ImageUrl,
                    bestAgainst,
                    worstAgainst,
                    counterStatusMessage),
                null);
        }
        catch (Exception ex)
        {
            return new PokemonDraftDetailsResponse(null, $"Database detail load failed: {GetExceptionMessage(ex)}");
        }
    }

    private static string GetExceptionMessage(Exception ex)
    {
        return ex.InnerException is null
            ? ex.Message
            : $"{ex.Message} Inner: {ex.InnerException.Message}";
    }
}
