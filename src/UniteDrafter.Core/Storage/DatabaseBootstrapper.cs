using Microsoft.Data.Sqlite;

namespace UniteDrafter.Storage;

public sealed record DatabaseStartupSummary(
    string DatabasePath,
    bool CreatedDatabaseFile,
    bool CreatedSchema);

public static class DatabaseBootstrapper
{
    public static DatabaseStartupSummary EnsureInitialized(
        string? databasePath = null,
        string? storageRootPath = null,
        string? startPath = null)
    {
        var resolvedDatabasePath = UniteDrafterStoragePaths.ResolveStoragePath(
            startPath ?? AppContext.BaseDirectory,
            storageRootPath,
            databasePath,
            UniteDrafterStoragePaths.DefaultDatabasePath);

        var databaseDirectory = Path.GetDirectoryName(resolvedDatabasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        var createdDatabaseFile = !File.Exists(resolvedDatabasePath);

        using var connection = new SqliteConnection($"Data Source={resolvedDatabasePath}");
        connection.Open();

        DatabaseSchemaManager.EnableForeignKeys(connection);

        var schemaValidation = DatabaseSchemaManager.ValidateSchema(connection);
        if (!schemaValidation.HasSchema)
        {
            DatabaseSchemaManager.EnsureSchema(connection);
        }
        else if (!schemaValidation.IsCompatible)
        {
            throw new InvalidOperationException(
                $"Database schema is incompatible at {resolvedDatabasePath}. {schemaValidation.ErrorMessage} Run rebuild-db or refresh-db to recreate it.");
        }

        return new DatabaseStartupSummary(
            resolvedDatabasePath,
            createdDatabaseFile,
            CreatedSchema: !schemaValidation.HasSchema);
    }
}
