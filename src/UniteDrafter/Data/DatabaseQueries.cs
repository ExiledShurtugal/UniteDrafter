using Microsoft.Data.Sqlite;

namespace UniteDrafter.Data;

public sealed record DatabaseSummary(long PokemonCount, long MatchupCount);

public sealed record PokemonMatchupResult(
    string PokemonName,
    string OpponentName,
    double WinRate);

public sealed record PokemonSearchResult(
    int UniteApiId,
    int? PokedexId,
    string PokemonName);

public static class DatabaseQueries
{
    public static DatabaseSummary GetDatabaseSummary(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT
    (SELECT COUNT(*) FROM pokemon),
    (SELECT COUNT(*) FROM pokemon_matchup);
""";

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new DatabaseSummary(0, 0);
        }

        return new DatabaseSummary(
            reader.GetInt64(0),
            reader.GetInt64(1));
    }

    public static IReadOnlyList<PokemonMatchupResult> GetMatchupPreview(SqliteConnection connection, int limit = 5)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT p.name, o.name, m.win_rate
FROM pokemon_matchup m
JOIN pokemon p ON p.uniteapi_id = m.pokemon_uniteapi_id
JOIN pokemon o ON o.uniteapi_id = m.opponent_uniteapi_id
ORDER BY p.name, o.name
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var results = new List<PokemonMatchupResult>();

        while (reader.Read())
        {
            results.Add(new PokemonMatchupResult(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDouble(2)));
        }

        return results;
    }

    public static IReadOnlyList<PokemonMatchupResult> GetMatchupsForPokemon(SqliteConnection connection, string pokemonName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT p.name, o.name, m.win_rate
FROM pokemon_matchup m
JOIN pokemon p ON p.uniteapi_id = m.pokemon_uniteapi_id
JOIN pokemon o ON o.uniteapi_id = m.opponent_uniteapi_id
WHERE lower(p.name) = lower($pokemonName)
ORDER BY m.win_rate DESC, o.name ASC;
""";
        command.Parameters.AddWithValue("$pokemonName", pokemonName);

        using var reader = command.ExecuteReader();
        var results = new List<PokemonMatchupResult>();

        while (reader.Read())
        {
            results.Add(new PokemonMatchupResult(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDouble(2)));
        }

        return results;
    }

    public static IReadOnlyList<PokemonSearchResult> SearchPokemon(SqliteConnection connection, string searchTerm, int limit = 10)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT uniteapi_id, pokedex_id, name
FROM pokemon
WHERE lower(name) LIKE '%' || lower($searchTerm) || '%'
ORDER BY
    CASE WHEN lower(name) = lower($searchTerm) THEN 0 ELSE 1 END,
    CASE WHEN lower(name) LIKE lower($searchTerm) || '%' THEN 0 ELSE 1 END,
    name ASC
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$searchTerm", searchTerm);
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var results = new List<PokemonSearchResult>();

        while (reader.Read())
        {
            results.Add(new PokemonSearchResult(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetString(2)));
        }

        return results;
    }

    public static void PrintDatabaseSummary(SqliteConnection connection)
    {
        var summary = GetDatabaseSummary(connection);
        Console.WriteLine($"pokemon rows: {summary.PokemonCount}, pokemon_matchup rows: {summary.MatchupCount}");

        foreach (var matchup in GetMatchupPreview(connection))
        {
            Console.WriteLine(
                $"sample matchup: {matchup.PokemonName} vs {matchup.OpponentName} -> {matchup.WinRate}");
        }
    }
}
