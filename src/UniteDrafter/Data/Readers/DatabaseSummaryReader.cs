namespace UniteDrafter.Data;

public sealed class DatabaseSummaryReader : SqliteDataReaderBase, IDatabaseSummaryReader
{
    public DatabaseSummaryReader(string databasePath)
        : base(databasePath)
    {
    }

    public DatabaseSummary GetDatabaseSummary()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT
    (SELECT COUNT(*) FROM pokemon) AS pokemon_count,
    (SELECT COUNT(*) FROM pokemon_matchup) AS matchup_count;
""";

        using var reader = command.ExecuteReader();
        reader.Read();
        return new DatabaseSummary(reader.GetInt64(0), reader.GetInt64(1));
    }

    public IReadOnlyList<PokemonMatchupResult> GetMatchupPreview(int limit = 10)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT p.name, o.name, pm.win_rate
FROM pokemon_matchup pm
JOIN pokemon p ON p.uniteapi_id = pm.pokemon_uniteapi_id
JOIN pokemon o ON o.uniteapi_id = pm.opponent_uniteapi_id
ORDER BY p.name ASC, pm.win_rate DESC, o.name ASC
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

    public void PrintDatabaseSummary(int previewLimit = 10)
    {
        var summary = GetDatabaseSummary();
        Console.WriteLine($"Pokemon count: {summary.PokemonCount}");
        Console.WriteLine($"Matchup count: {summary.MatchupCount}");

        var matchupPreview = GetMatchupPreview(previewLimit);
        if (matchupPreview.Count == 0)
        {
            return;
        }

        Console.WriteLine("Sample matchups:");
        foreach (var matchup in matchupPreview)
        {
            Console.WriteLine($"- {matchup.PokemonName} vs {matchup.OpponentName}: {matchup.WinRate:F1}%");
        }
    }

}
