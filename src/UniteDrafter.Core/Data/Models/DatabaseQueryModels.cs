namespace UniteDrafter.Data;

public sealed record DatabaseSummary(long PokemonCount, long MatchupCount);

public sealed record PokemonMatchupResult(
    string PokemonName,
    string OpponentName,
    double WinRate,
    int OpponentUniteApiId = 0);

public sealed record PokemonSearchResult(
    int UniteApiId,
    int? PokedexId,
    string PokemonName);

public sealed record PokemonProfileResult(
    int UniteApiId,
    int? PokedexId,
    string PokemonName,
    string ImageUrl);
