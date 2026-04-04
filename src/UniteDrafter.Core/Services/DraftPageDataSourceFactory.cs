using UniteDrafter.Data;

namespace UniteDrafter.Services;

public static class DraftPageDataSourceFactory
{
    public static IDraftPageDataSource Create(
        string contentRootPath,
        string? configuredDatabasePath,
        string? configuredStorageRootPath = null)
    {
        var databasePath = DraftPagePathResolver.ResolveDatabasePath(
            contentRootPath,
            configuredStorageRootPath,
            configuredDatabasePath);

        return CreateFromResolvedDatabasePath(databasePath);
    }

    public static IDraftPageDataSource CreateForWorkingDirectory(
        string startPath,
        string? configuredDatabasePath = null,
        string? configuredStorageRootPath = null)
    {
        var databasePath = DraftPagePathResolver.ResolveDatabasePath(
            startPath,
            configuredStorageRootPath,
            configuredDatabasePath);

        return CreateFromResolvedDatabasePath(databasePath);
    }

    private static IDraftPageDataSource CreateFromResolvedDatabasePath(string databasePath)
    {
        var pokemonDataReader = new PokemonDataReader(databasePath);
        var pokemonMatchupDataReader = new PokemonMatchupDataReader(databasePath);
        return new SqliteDraftPageDataSource(databasePath, pokemonDataReader, pokemonMatchupDataReader);
    }
}
