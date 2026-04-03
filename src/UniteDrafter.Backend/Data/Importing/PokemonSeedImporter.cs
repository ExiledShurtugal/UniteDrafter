using Microsoft.Data.Sqlite;
using UniteDrafter.Decrypter;

namespace UniteDrafter.Data;

public sealed record SeedImportFailure(string FilePath, string Error);

public sealed record SeedImportSummary(
    int ParsedFiles,
    int SkippedFiles,
    int PokemonUpserts,
    int MatchupUpserts,
    IReadOnlyList<string> MissingDirectories,
    IReadOnlyList<SeedImportFailure> Failures);

public static class PokemonSeedImporter
{
    public static SeedImportSummary ImportFromDirectories(
        SqliteConnection connection,
        IEnumerable<string> jsonSourceDirectories)
    {
        var parsedFiles = 0;
        var skippedFiles = 0;
        var pokemonUpserts = 0;
        var matchupUpserts = 0;
        var missingDirectories = new List<string>();
        var failures = new List<SeedImportFailure>();

        using var transaction = connection.BeginTransaction();

        foreach (var sourceDirectory in jsonSourceDirectories)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                missingDirectories.Add(sourceDirectory);
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                ImportSingleFile(
                    connection,
                    transaction,
                    filePath,
                    ref parsedFiles,
                    ref skippedFiles,
                    ref pokemonUpserts,
                    ref matchupUpserts,
                    failures);
            }
        }

        transaction.Commit();

        return new SeedImportSummary(
            parsedFiles,
            skippedFiles,
            pokemonUpserts,
            matchupUpserts,
            missingDirectories,
            failures);
    }

    private static void ImportSingleFile(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string filePath,
        ref int parsedFiles,
        ref int skippedFiles,
        ref int pokemonUpserts,
        ref int matchupUpserts,
        List<SeedImportFailure> failures)
    {
        if (!TryReadPokemonWinRates(filePath, out var pokemonWinRates, out var error))
        {
            skippedFiles++;
            failures.Add(new SeedImportFailure(filePath, error ?? "Unknown parse failure."));
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

    private static bool TryReadPokemonWinRates(
        string filePath,
        out PokemonCountersWinRates? pokemonWinRates,
        out string? error)
    {
        pokemonWinRates = null;
        error = null;

        try
        {
            var fileText = File.ReadAllText(filePath);
            using var doc = System.Text.Json.JsonDocument.Parse(fileText);
            var root = doc.RootElement;

            pokemonWinRates = root.TryGetProperty("pageProps", out _) || root.TryGetProperty("props", out _)
                ? BestBuildsReader.ReadPokemonWinRatesFromEncryptedPageFile(filePath)
                : BestBuildsReader.ReadPokemonWinRatesFromDecryptedJsonFile(filePath);

            if (pokemonWinRates.Pokemon.UniteApiId > 0)
            {
                return true;
            }

            error = "Parsed file but Unite API id was missing or invalid.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
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
