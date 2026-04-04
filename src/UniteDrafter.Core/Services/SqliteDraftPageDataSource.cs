using UniteDrafter.Data;

namespace UniteDrafter.Services;

public sealed class SqliteDraftPageDataSource : IDraftPageDataSource
{
    private readonly string databasePath;
    private readonly IPokemonDataReader pokemonDataReader;
    private readonly IPokemonMatchupDataReader pokemonMatchupDataReader;

    public SqliteDraftPageDataSource(
        string databasePath,
        IPokemonDataReader pokemonDataReader,
        IPokemonMatchupDataReader pokemonMatchupDataReader)
    {
        this.databasePath = Path.GetFullPath(databasePath);
        this.pokemonDataReader = pokemonDataReader;
        this.pokemonMatchupDataReader = pokemonMatchupDataReader;
    }

    public string? GetAvailabilityError()
    {
        return File.Exists(databasePath)
            ? null
            : $"Database file not found at: {databasePath}";
    }

    public IReadOnlyList<PokemonSearchResult> GetAllPokemon() =>
        pokemonDataReader.GetAllPokemon();

    public IReadOnlyList<PokemonSearchResult> SearchPokemon(string searchTerm, int limit = 8) =>
        pokemonDataReader.SearchPokemon(searchTerm, limit);

    public PokemonProfileResult? GetPokemonProfile(string pokemonName) =>
        pokemonDataReader.GetPokemonProfile(pokemonName);

    public IReadOnlyList<PokemonMatchupResult> GetMatchupsForPokemon(string pokemonName) =>
        pokemonMatchupDataReader.GetMatchupsForPokemon(pokemonName);

    public IReadOnlyList<PokemonMatchupResult> GetMatchupsForPokemon(int uniteApiId) =>
        pokemonMatchupDataReader.GetMatchupsForPokemon(uniteApiId);
}
