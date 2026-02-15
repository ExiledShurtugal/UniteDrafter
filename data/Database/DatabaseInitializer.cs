using System;
using Microsoft.Data.Sqlite;

namespace UniteDrafter.Data
{
    public static class DatabaseInitializer
    {
        private const string DbFile = "data/Database/game_data.db";

        public static void Initialize()
        {
            var connectionString = $"Data Source={DbFile}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // Create Matches table
            var createMatchesTable = @"
            CREATE TABLE IF NOT EXISTS Matches (
                MatchID TEXT PRIMARY KEY,
                WinPokemon1 TEXT,
                WinPokemon2 TEXT,
                WinPokemon3 TEXT,
                WinPokemon4 TEXT,
                WinPokemon5 TEXT,
                LosePokemon1 TEXT,
                LosePokemon2 TEXT,
                LosePokemon3 TEXT,
                LosePokemon4 TEXT,
                LosePokemon5 TEXT
            );";

            using var cmd1 = new SqliteCommand(createMatchesTable, connection);
            cmd1.ExecuteNonQuery();

            // Create Pokemon table
            var createPokemonTable = @"
            CREATE TABLE IF NOT EXISTS Pokemon (
                Name TEXT PRIMARY KEY
            );";

            using var cmd2 = new SqliteCommand(createPokemonTable, connection);
            cmd2.ExecuteNonQuery();

            // Create PokemonMatchups table
            var createMatchupsTable = @"
            CREATE TABLE IF NOT EXISTS PokemonMatchups (
                PokemonName TEXT,
                OpponentName TEXT,
                NumberMatches INTEGER,
                Wins INTEGER,
                PRIMARY KEY (PokemonName, OpponentName),
                FOREIGN KEY (PokemonName) REFERENCES Pokemon(Name),
                FOREIGN KEY (OpponentName) REFERENCES Pokemon(Name)
            );";

            using var cmd3 = new SqliteCommand(createMatchupsTable, connection);
            cmd3.ExecuteNonQuery();

            Console.WriteLine("Database and tables initialized successfully!");
        }
    }
}
