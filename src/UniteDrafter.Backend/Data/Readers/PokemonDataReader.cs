using Microsoft.Data.Sqlite;

namespace UniteDrafter.Data;

public sealed class PokemonDataReader : SqliteDataReaderBase, IPokemonDataReader
{
    public PokemonDataReader(string databasePath)
        : base(databasePath)
    {
    }

    public IReadOnlyList<PokemonSearchResult> SearchPokemon(string searchTerm, int limit = 8)
    {
        using var connection = OpenConnection();
        return ReadSearchResults(connection, searchTerm.Trim(), limit);
    }

    public PokemonProfileResult? GetPokemonProfile(string pokemonName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT uniteapi_id, pokedex_id, name, img
FROM pokemon
WHERE lower(name) = lower($pokemonName)
LIMIT 1;
""";
        command.Parameters.AddWithValue("$pokemonName", pokemonName.Trim());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PokemonProfileResult(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    private static IReadOnlyList<PokemonSearchResult> ReadSearchResults(SqliteConnection connection, string searchTerm, int limit)
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
}
