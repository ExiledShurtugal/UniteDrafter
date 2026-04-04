using UniteDrafter.Data;

namespace UniteDrafter.Services;

public interface IDraftPageDataSource
{
    string? GetAvailabilityError();
    IReadOnlyList<PokemonSearchResult> SearchPokemon(string searchTerm, int limit = 8);
    PokemonProfileResult? GetPokemonProfile(string pokemonName);
    IReadOnlyList<PokemonMatchupResult> GetMatchupsForPokemon(string pokemonName);
}
