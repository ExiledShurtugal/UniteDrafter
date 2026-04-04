using Microsoft.Data.Sqlite;
using UniteDrafter.Data;
using UniteDrafter.Storage;

namespace UniteDrafter.SourceUpdate.Data;

public static class DatabaseRebuilder
{
    public static SeedImportSummary RebuildFromSources(
        string? databasePath = null,
        IEnumerable<string>? jsonSourceDirectories = null,
        string? storageRootPath = null,
        string? startPath = null)
    {
        var resolvedDatabasePath = ResolveDatabasePath(startPath, storageRootPath, databasePath);
        var resolvedJsonSourceDirectories = ResolveJsonSourceDirectories(startPath, storageRootPath, jsonSourceDirectories);

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

    private static string ResolveDatabasePath(string? startPath, string? storageRootPath, string? databasePath)
    {
        return UniteDrafterStoragePaths.ResolveStoragePath(
            startPath ?? AppContext.BaseDirectory,
            storageRootPath,
            databasePath,
            UniteDrafterStoragePaths.DefaultDatabasePath);
    }

    private static IReadOnlyList<string> ResolveJsonSourceDirectories(
        string? startPath,
        string? storageRootPath,
        IEnumerable<string>? jsonSourceDirectories)
    {
        var rootPath = UniteDrafterStoragePaths.ResolveStorageRoot(
            startPath ?? AppContext.BaseDirectory,
            storageRootPath);

        return jsonSourceDirectories?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => UniteDrafterStoragePaths.ResolvePath(rootPath, path))
            .ToArray()
            ?? [UniteDrafterStoragePaths.ResolvePath(rootPath, UniteDrafterStoragePaths.DefaultGuideSourcesDirectory)];
    }
}
