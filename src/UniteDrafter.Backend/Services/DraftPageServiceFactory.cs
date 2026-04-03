using UniteDrafter.Data;

namespace UniteDrafter.Services;

public static class DraftPageServiceFactory
{
    private static readonly string DefaultDatabasePath =
        Path.Combine("..", "..", "data", "Database", "unitedrafter.db");

    public static IDraftPageService Create(string contentRootPath, string? configuredDatabasePath)
    {
        var relativePath = string.IsNullOrWhiteSpace(configuredDatabasePath)
            ? DefaultDatabasePath
            : configuredDatabasePath;
        var databasePath = Path.GetFullPath(Path.Combine(contentRootPath, relativePath));

        var pokemonDataReader = new PokemonDataReader(databasePath);
        var pokemonMatchupDataReader = new PokemonMatchupDataReader(databasePath);
        var dataSource = new SqliteDraftPageDataSource(databasePath, pokemonDataReader, pokemonMatchupDataReader);

        return new DraftPageService(dataSource);
    }
}
