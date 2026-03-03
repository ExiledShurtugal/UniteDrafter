using Microsoft.Data.Sqlite;
using UniteDrafter.Decrypter;

namespace UniteDrafter.Data;

public static class DatabaseInitializer
{
    private const string DatabasePath = "data/Database/unitedrafter.db";
    private static readonly string[] JsonSourceDirectories =
    [
        "data/JsonsManually/Players",
        "data/Database/JsonsManually",
        "notes/JsonExamples"
    ];

    public static void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

        using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        EnableForeignKeys(connection);
        CreateSchema(connection);
        SeedFromJsonFiles(connection);
        PrintDatabaseSummary(connection);
    }

    private static void EnableForeignKeys(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    private static void CreateSchema(SqliteConnection connection)
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

    private static void SeedFromJsonFiles(SqliteConnection connection)
    {
        var parsedFiles = 0;
        var pokemonUpserts = 0;
        var matchupUpserts = 0;

        using var transaction = connection.BeginTransaction();

        foreach (var sourceDirectory in JsonSourceDirectories)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                SeedSingleFile(connection, transaction, filePath, ref parsedFiles, ref pokemonUpserts, ref matchupUpserts);
            }
        }

        transaction.Commit();

        Console.WriteLine(
            $"Database seed complete. Parsed files: {parsedFiles}, Pokemon upserts: {pokemonUpserts}, Matchup upserts: {matchupUpserts}");
    }

    private static void SeedSingleFile(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string filePath,
        ref int parsedFiles,
        ref int pokemonUpserts,
        ref int matchupUpserts)
    {
        if (!TryReadPokemonWinRates(filePath, out var pokemonWinRates))
        {
            return;
        }
        parsedFiles++;

        UpsertPokemon(
            connection,
            transaction,
            pokemonWinRates!.Pokemon.UniteApiId,
            pokemonWinRates.Pokemon.PokedexId,
            pokemonWinRates.Pokemon.PokemonName,
            pokemonWinRates.Pokemon.PokemonImg);
        pokemonUpserts++;

        if (!pokemonWinRates.CounterSections.TryGetValue("all", out var allMatchups))
        {
            return;
        }

        foreach (var matchup in allMatchups)
        {
            UpsertPokemon(connection, transaction, matchup.OpponentUniteApiId, null, matchup.OpponentPokemonName, matchup.OpponentPokemonImg);
            UpsertMatchup(connection, transaction, pokemonWinRates.Pokemon.UniteApiId, matchup.OpponentUniteApiId, matchup.WinRate);
            pokemonUpserts++;
            matchupUpserts++;
        }
    }

    private static bool TryReadPokemonWinRates(string filePath, out PokemonCountersWinRates? pokemonWinRates)
    {
        pokemonWinRates = null;

        try
        {
            var fileText = File.ReadAllText(filePath);
            using var doc = System.Text.Json.JsonDocument.Parse(fileText);
            var root = doc.RootElement;

            pokemonWinRates = root.TryGetProperty("pageProps", out _) || root.TryGetProperty("props", out _)
                ? BestBuildsReader.ReadPokemonWinRatesFromEncryptedPageFile(filePath)
                : BestBuildsReader.ReadPokemonWinRatesFromDecryptedJsonFile(filePath);

            return pokemonWinRates.Pokemon.UniteApiId > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void PrintDatabaseSummary(SqliteConnection connection)
    {
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = @"
SELECT
    (SELECT COUNT(*) FROM pokemon),
    (SELECT COUNT(*) FROM pokemon_matchup);
";

        using var reader = countCmd.ExecuteReader();
        if (reader.Read())
        {
            Console.WriteLine($"pokemon rows: {reader.GetInt64(0)}, pokemon_matchup rows: {reader.GetInt64(1)}");
        }

        using var sampleCmd = connection.CreateCommand();
        sampleCmd.CommandText = @"
SELECT p.name, o.name, m.win_rate
FROM pokemon_matchup m
JOIN pokemon p ON p.uniteapi_id = m.pokemon_uniteapi_id
JOIN pokemon o ON o.uniteapi_id = m.opponent_uniteapi_id
ORDER BY p.name, o.name
LIMIT 5;
";

        using var sampleReader = sampleCmd.ExecuteReader();
        while (sampleReader.Read())
        {
            Console.WriteLine(
                $"sample matchup: {sampleReader.GetString(0)} vs {sampleReader.GetString(1)} -> {sampleReader.GetDouble(2)}");
        }
    }

    private static void UpsertPokemon(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int uniteApiId,
        int? pokedexId,
        string name,
        string img)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
INSERT INTO pokemon (uniteapi_id, pokedex_id, name, img)
VALUES ($uniteApiId, $pokedexId, $name, $img)
ON CONFLICT(uniteapi_id) DO UPDATE SET
    pokedex_id = COALESCE(excluded.pokedex_id, pokemon.pokedex_id),
    name = excluded.name,
    img = excluded.img;
";
        cmd.Parameters.AddWithValue("$uniteApiId", uniteApiId);
        if (pokedexId.HasValue)
        {
            cmd.Parameters.AddWithValue("$pokedexId", pokedexId.Value);
        }
        else
        {
            cmd.Parameters.AddWithValue("$pokedexId", DBNull.Value);
        }
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$img", img);
        cmd.ExecuteNonQuery();
    }

    private static void UpsertMatchup(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int pokemonUniteApiId,
        int opponentUniteApiId,
        double winRate)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
INSERT INTO pokemon_matchup (pokemon_uniteapi_id, opponent_uniteapi_id, win_rate)
VALUES ($pokemonUniteApiId, $opponentUniteApiId, $winRate)
ON CONFLICT(pokemon_uniteapi_id, opponent_uniteapi_id) DO UPDATE SET
    win_rate = excluded.win_rate;
";
        cmd.Parameters.AddWithValue("$pokemonUniteApiId", pokemonUniteApiId);
        cmd.Parameters.AddWithValue("$opponentUniteApiId", opponentUniteApiId);
        cmd.Parameters.AddWithValue("$winRate", winRate);
        cmd.ExecuteNonQuery();
    }
}
