namespace UniteDrafter.Data;

public sealed class PokemonMatchupDataReader : SqliteDataReaderBase, IPokemonMatchupDataReader
{
    public PokemonMatchupDataReader(string databasePath)
        : base(databasePath)
    {
    }

    public IReadOnlyList<PokemonMatchupResult> GetMatchupsForPokemon(string pokemonName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT p.name, o.name, pm.win_rate
FROM pokemon_matchup pm
JOIN pokemon p ON p.uniteapi_id = pm.pokemon_uniteapi_id
JOIN pokemon o ON o.uniteapi_id = pm.opponent_uniteapi_id
WHERE lower(p.name) = lower($pokemonName)
ORDER BY pm.win_rate DESC, o.name ASC;
""";
        command.Parameters.AddWithValue("$pokemonName", pokemonName.Trim());

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

}
