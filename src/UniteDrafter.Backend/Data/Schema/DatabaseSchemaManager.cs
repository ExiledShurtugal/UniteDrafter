using Microsoft.Data.Sqlite;

namespace UniteDrafter.Data;

public static class DatabaseSchemaManager
{
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
}
