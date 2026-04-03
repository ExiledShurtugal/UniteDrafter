using UniteDrafter.Data;

namespace UniteDrafter.Services;

public sealed record PokemonDraftDetails(
    int UniteApiId,
    int? PokedexId,
    string PokemonName,
    string ImageUrl,
    IReadOnlyList<PokemonMatchupResult> BestAgainst,
    IReadOnlyList<PokemonMatchupResult> WorstAgainst,
    string? CounterStatusMessage);

public sealed record PokemonSearchResponse(
    IReadOnlyList<PokemonSearchResult> Results,
    string? ErrorMessage);

public sealed record PokemonDraftDetailsResponse(
    PokemonDraftDetails? Details,
    string? ErrorMessage);

public interface IDraftPageService
{
    PokemonSearchResponse SearchPokemon(string searchTerm, int limit = 8);
    PokemonDraftDetailsResponse GetPokemonDraftDetails(string pokemonName, int matchupLimit = 5);
}
