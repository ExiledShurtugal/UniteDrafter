using Microsoft.Data.Sqlite;

namespace UniteDrafter.Storage;

public sealed record DatabaseSchemaValidationResult(
    bool HasSchema,
    bool IsCompatible,
    string? ErrorMessage);

public static class DatabaseSchemaManager
{
    public static void EnsureSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS pokemon (
    uniteapi_id INTEGER PRIMARY KEY,
    pokedex_id INTEGER,
    name TEXT NOT NULL,
    img TEXT NOT NULL,
    UNIQUE (pokedex_id)
);

CREATE TABLE IF NOT EXISTS pokemon_matchup (
    pokemon_uniteapi_id INTEGER NOT NULL,
    opponent_uniteapi_id INTEGER NOT NULL,
    win_rate REAL NOT NULL,
    PRIMARY KEY (pokemon_uniteapi_id, opponent_uniteapi_id),
    FOREIGN KEY (pokemon_uniteapi_id) REFERENCES pokemon(uniteapi_id),
    FOREIGN KEY (opponent_uniteapi_id) REFERENCES pokemon(uniteapi_id),
    CHECK (win_rate >= 0 AND win_rate <= 100)
);

CREATE INDEX IF NOT EXISTS idx_pokemon_name ON pokemon(name);
CREATE INDEX IF NOT EXISTS idx_matchup_opponent ON pokemon_matchup(opponent_uniteapi_id);
";
        cmd.ExecuteNonQuery();
    }

    public static void EnableForeignKeys(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public static void RecreateSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
DROP TABLE IF EXISTS pokemon_matchup;
DROP TABLE IF EXISTS pokemon;

CREATE TABLE pokemon (
    uniteapi_id INTEGER PRIMARY KEY,
    pokedex_id INTEGER,
    name TEXT NOT NULL,
    img TEXT NOT NULL,
    UNIQUE (pokedex_id)
);

CREATE TABLE pokemon_matchup (
    pokemon_uniteapi_id INTEGER NOT NULL,
    opponent_uniteapi_id INTEGER NOT NULL,
    win_rate REAL NOT NULL,
    PRIMARY KEY (pokemon_uniteapi_id, opponent_uniteapi_id),
    FOREIGN KEY (pokemon_uniteapi_id) REFERENCES pokemon(uniteapi_id),
    FOREIGN KEY (opponent_uniteapi_id) REFERENCES pokemon(uniteapi_id),
    CHECK (win_rate >= 0 AND win_rate <= 100)
);

CREATE INDEX IF NOT EXISTS idx_pokemon_name ON pokemon(name);
CREATE INDEX IF NOT EXISTS idx_matchup_opponent ON pokemon_matchup(opponent_uniteapi_id);
";
        cmd.ExecuteNonQuery();
    }

    public static bool HasRequiredSchema(SqliteConnection connection)
    {
        var validation = ValidateSchema(connection);
        return validation.HasSchema && validation.IsCompatible;
    }

    public static DatabaseSchemaValidationResult ValidateSchema(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var tableNames = GetUserTableNames(connection);
        var hasPokemonTable = tableNames.Contains("pokemon");
        var hasMatchupTable = tableNames.Contains("pokemon_matchup");

        if (!hasPokemonTable && !hasMatchupTable)
        {
            return new DatabaseSchemaValidationResult(
                HasSchema: false,
                IsCompatible: true,
                ErrorMessage: null);
        }

        if (!hasPokemonTable || !hasMatchupTable)
        {
            return new DatabaseSchemaValidationResult(
                HasSchema: true,
                IsCompatible: false,
                ErrorMessage: "Expected both pokemon and pokemon_matchup tables to exist together.");
        }

        if (!HasExpectedColumns(connection, "pokemon",
            [
                new ExpectedColumn("uniteapi_id", "INTEGER", PrimaryKeyPosition: 1, NotNull: null),
                new ExpectedColumn("pokedex_id", "INTEGER", PrimaryKeyPosition: 0, NotNull: false),
                new ExpectedColumn("name", "TEXT", PrimaryKeyPosition: 0, NotNull: true),
                new ExpectedColumn("img", "TEXT", PrimaryKeyPosition: 0, NotNull: true)
            ],
            out var columnError))
        {
            return new DatabaseSchemaValidationResult(true, false, columnError);
        }

        if (!HasExpectedColumns(connection, "pokemon_matchup",
            [
                new ExpectedColumn("pokemon_uniteapi_id", "INTEGER", PrimaryKeyPosition: 1, NotNull: true),
                new ExpectedColumn("opponent_uniteapi_id", "INTEGER", PrimaryKeyPosition: 2, NotNull: true),
                new ExpectedColumn("win_rate", "REAL", PrimaryKeyPosition: 0, NotNull: true)
            ],
            out columnError))
        {
            return new DatabaseSchemaValidationResult(true, false, columnError);
        }

        if (!HasNamedIndex(connection, "pokemon", "idx_pokemon_name"))
        {
            return new DatabaseSchemaValidationResult(
                true,
                false,
                "Missing expected index idx_pokemon_name on pokemon(name).");
        }

        if (!HasNamedIndex(connection, "pokemon_matchup", "idx_matchup_opponent"))
        {
            return new DatabaseSchemaValidationResult(
                true,
                false,
                "Missing expected index idx_matchup_opponent on pokemon_matchup(opponent_uniteapi_id).");
        }

        if (!HasUniqueIndexOnColumns(connection, "pokemon", ["pokedex_id"]))
        {
            return new DatabaseSchemaValidationResult(
                true,
                false,
                "Missing expected unique constraint on pokemon(pokedex_id).");
        }

        if (!HasExpectedForeignKeys(connection))
        {
            return new DatabaseSchemaValidationResult(
                true,
                false,
                "pokemon_matchup is missing one or more required foreign key constraints.");
        }

        if (!HasExpectedWinRateCheckConstraint(connection))
        {
            return new DatabaseSchemaValidationResult(
                true,
                false,
                "pokemon_matchup is missing the expected win_rate range check constraint.");
        }

        return new DatabaseSchemaValidationResult(
            HasSchema: true,
            IsCompatible: true,
            ErrorMessage: null);
    }

    private static HashSet<string> GetUserTableNames(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
SELECT name
FROM sqlite_master
WHERE type = 'table'
  AND name NOT LIKE 'sqlite_%';
""";

        using var reader = cmd.ExecuteReader();
        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static bool HasExpectedColumns(
        SqliteConnection connection,
        string tableName,
        IReadOnlyList<ExpectedColumn> expectedColumns,
        out string errorMessage)
    {
        var actualColumns = ReadTableColumns(connection, tableName);
        if (actualColumns.Count != expectedColumns.Count)
        {
            errorMessage =
                $"Table {tableName} has {actualColumns.Count} columns but expected {expectedColumns.Count}.";
            return false;
        }

        for (var i = 0; i < expectedColumns.Count; i++)
        {
            var expected = expectedColumns[i];
            var actual = actualColumns[i];

            if (!string.Equals(actual.Name, expected.Name, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage =
                    $"Table {tableName} column #{i + 1} is {actual.Name} but expected {expected.Name}.";
                return false;
            }

            if (!string.Equals(actual.Type, expected.Type, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage =
                    $"Table {tableName}.{expected.Name} has type {actual.Type} but expected {expected.Type}.";
                return false;
            }

            if (actual.PrimaryKeyPosition != expected.PrimaryKeyPosition)
            {
                errorMessage =
                    $"Table {tableName}.{expected.Name} has primary key position {actual.PrimaryKeyPosition} but expected {expected.PrimaryKeyPosition}.";
                return false;
            }

            if (expected.NotNull.HasValue && actual.NotNull != expected.NotNull.Value)
            {
                errorMessage =
                    $"Table {tableName}.{expected.Name} not-null flag was {actual.NotNull} but expected {expected.NotNull.Value}.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static List<TableColumn> ReadTableColumns(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''")}');";

        using var reader = cmd.ExecuteReader();
        var columns = new List<TableColumn>();
        while (reader.Read())
        {
            columns.Add(new TableColumn(
                reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.GetInt32(3) != 0,
                reader.GetInt32(5)));
        }

        return columns;
    }

    private static bool HasNamedIndex(SqliteConnection connection, string tableName, string indexName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'index'
  AND tbl_name = $tableName
  AND name = $indexName;
""";
        cmd.Parameters.AddWithValue("$tableName", tableName);
        cmd.Parameters.AddWithValue("$indexName", indexName);

        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
    }

    private static bool HasUniqueIndexOnColumns(
        SqliteConnection connection,
        string tableName,
        IReadOnlyList<string> expectedColumns)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA index_list('{tableName.Replace("'", "''")}');";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var indexName = reader.GetString(1);
            var isUnique = reader.GetInt32(2) != 0;
            if (!isUnique)
            {
                continue;
            }

            var indexColumns = ReadIndexColumns(connection, indexName);
            if (indexColumns.SequenceEqual(expectedColumns, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ReadIndexColumns(SqliteConnection connection, string indexName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA index_info('{indexName.Replace("'", "''")}');";

        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(2));
        }

        return columns;
    }

    private static bool HasExpectedForeignKeys(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_list('pokemon_matchup');";

        using var reader = cmd.ExecuteReader();
        var foreignKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            foreignKeys.Add($"{reader.GetString(3)}->{reader.GetString(2)}.{reader.GetString(4)}");
        }

        return foreignKeys.SetEquals(
        [
            "pokemon_uniteapi_id->pokemon.uniteapi_id",
            "opponent_uniteapi_id->pokemon.uniteapi_id"
        ]);
    }

    private static bool HasExpectedWinRateCheckConstraint(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
SELECT sql
FROM sqlite_master
WHERE type = 'table'
  AND name = 'pokemon_matchup';
""";

        var sql = Convert.ToString(cmd.ExecuteScalar());
        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var normalizedSql = string.Concat(sql.Where(character => !char.IsWhiteSpace(character)))
            .ToLowerInvariant();
        return normalizedSql.Contains("check(win_rate>=0andwin_rate<=100)");
    }

    private sealed record ExpectedColumn(
        string Name,
        string Type,
        int PrimaryKeyPosition,
        bool? NotNull);

    private sealed record TableColumn(
        string Name,
        string Type,
        bool NotNull,
        int PrimaryKeyPosition);
}
