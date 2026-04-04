using UniteDrafter.Data;

namespace UniteDrafter.Services;

public sealed class DraftPageService : IDraftPageService
{
    private readonly IDraftPageDataSource dataSource;

    public DraftPageService(IDraftPageDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public PokemonRosterResponse GetAllPokemon()
    {
        var availabilityError = dataSource.GetAvailabilityError();
        if (!string.IsNullOrWhiteSpace(availabilityError))
        {
            return new PokemonRosterResponse([], availabilityError);
        }

        try
        {
            var results = dataSource.GetAllPokemon();
            return new PokemonRosterResponse(results, null);
        }
        catch (Exception ex)
        {
            return new PokemonRosterResponse([], $"Database roster load failed: {GetExceptionMessage(ex)}");
        }
    }

    public PokemonSearchResponse SearchPokemon(string searchTerm, int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new PokemonSearchResponse([], null);
        }

        var availabilityError = dataSource.GetAvailabilityError();
        if (!string.IsNullOrWhiteSpace(availabilityError))
        {
            return new PokemonSearchResponse([], availabilityError);
        }

        try
        {
            var results = dataSource.SearchPokemon(searchTerm, limit);
            return new PokemonSearchResponse(results, null);
        }
        catch (Exception ex)
        {
            return new PokemonSearchResponse([], $"Database search failed: {GetExceptionMessage(ex)}");
        }
    }

    public PokemonDraftDetailsResponse GetPokemonDraftDetails(string pokemonName, int matchupLimit = 5)
    {
        if (string.IsNullOrWhiteSpace(pokemonName))
        {
            return new PokemonDraftDetailsResponse(null, "Pokemon name is required.");
        }

        var availabilityError = dataSource.GetAvailabilityError();
        if (!string.IsNullOrWhiteSpace(availabilityError))
        {
            return new PokemonDraftDetailsResponse(null, availabilityError);
        }

        try
        {
            var profile = dataSource.GetPokemonProfile(pokemonName);
            if (profile is null)
            {
                return new PokemonDraftDetailsResponse(null, $"Could not find Pokemon details for \"{pokemonName}\".");
            }

            var matchups = dataSource.GetMatchupsForPokemon(profile.UniteApiId);
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
                    matchups,
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
