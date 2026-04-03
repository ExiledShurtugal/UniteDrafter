using Microsoft.Data.Sqlite;
namespace UniteDrafter.Data;

public static class DatabaseInitializer
{
    private const string DefaultDatabasePath = "data/Database/unitedrafter.db";
    private static readonly string[] DefaultJsonSourceDirectories =
    [
        "data/Database/GuideSources"
    ];

    public static SeedImportSummary Initialize(
        string? databasePath = null,
        IEnumerable<string>? jsonSourceDirectories = null)
    {
        var resolvedDatabasePath = ResolveDatabasePath(databasePath);
        var resolvedJsonSourceDirectories = ResolveJsonSourceDirectories(jsonSourceDirectories);

        var databaseDirectory = Path.GetDirectoryName(resolvedDatabasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        using var connection = new SqliteConnection($"Data Source={resolvedDatabasePath}");
        connection.Open();

        DatabaseSchemaManager.EnableForeignKeys(connection);
        DatabaseSchemaManager.RecreateSchema(connection);

        var summary = PokemonSeedImporter.ImportFromDirectories(connection, resolvedJsonSourceDirectories);
        Console.WriteLine(
            $"Database seed complete. Parsed files: {summary.ParsedFiles}, Skipped files: {summary.SkippedFiles}, Pokemon upserts: {summary.PokemonUpserts}, Matchup upserts: {summary.MatchupUpserts}");

        if (summary.MissingDirectories.Count > 0)
        {
            Console.WriteLine("Missing source directories:");
            foreach (var directory in summary.MissingDirectories)
            {
                Console.WriteLine($"- {directory}");
            }
        }

        if (summary.Failures.Count > 0)
        {
            Console.WriteLine("Skipped source files:");
            foreach (var failure in summary.Failures)
            {
                Console.WriteLine($"- {failure.FilePath}: {failure.Error}");
            }
        }

        new DatabaseSummaryReader(resolvedDatabasePath).PrintDatabaseSummary();
        return summary;
    }

    private static string ResolveDatabasePath(string? databasePath)
    {
        return string.IsNullOrWhiteSpace(databasePath) ? DefaultDatabasePath : databasePath;
    }

    private static IReadOnlyList<string> ResolveJsonSourceDirectories(IEnumerable<string>? jsonSourceDirectories)
    {
        return jsonSourceDirectories?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray()
            ?? DefaultJsonSourceDirectories;
    }
}
