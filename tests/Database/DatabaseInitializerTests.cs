using System.Text.Json;
using Microsoft.Data.Sqlite;
using UniteDrafter.Data;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "UniteDrafter.DatabaseTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Initialize_CreatesSchemaAndSeedsPokemonAndMatchups()
    {
        var databasePath = Path.Combine(_tempRoot, "db", "unitedrafter.test.db");
        var seedDirectory = Path.Combine(_tempRoot, "seed");

        WriteSeedFile(seedDirectory, "blastoise.json", CreatePokemonPayload(
            uniteApiId: 180007,
            pokedexId: 7,
            pokemonName: "Blastoise",
            pokemonImg: "blastoise.png",
            matchups:
            [
                CreateMatchup(180006, "Charizard", "charizard.png", 52.5),
                CreateMatchup(180025, "Pikachu", "pikachu.png", 48.1)
            ]));

        DatabaseInitializer.Initialize(databasePath, [seedDirectory]);

        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT
    (SELECT COUNT(*) FROM pokemon),
    (SELECT COUNT(*) FROM pokemon_matchup),
    (SELECT name FROM pokemon WHERE uniteapi_id = 180007),
    (SELECT win_rate FROM pokemon_matchup WHERE pokemon_uniteapi_id = 180007 AND opponent_uniteapi_id = 180006);
""";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(3L, reader.GetInt64(0));
        Assert.Equal(2L, reader.GetInt64(1));
        Assert.Equal("Blastoise", reader.GetString(2));
        Assert.Equal(52.5, reader.GetDouble(3), 3);
    }

    [Fact]
    public void Initialize_RecreatesSchemaOnEachRun()
    {
        var databasePath = Path.Combine(_tempRoot, "db", "unitedrafter.test.db");
        var firstSeedDirectory = Path.Combine(_tempRoot, "seed-first");
        var secondSeedDirectory = Path.Combine(_tempRoot, "seed-second");

        WriteSeedFile(firstSeedDirectory, "first.json", CreatePokemonPayload(
            uniteApiId: 180007,
            pokedexId: 7,
            pokemonName: "Blastoise",
            pokemonImg: "blastoise.png",
            matchups:
            [
                CreateMatchup(180006, "Charizard", "charizard.png", 52.5)
            ]));

        WriteSeedFile(secondSeedDirectory, "second.json", CreatePokemonPayload(
            uniteApiId: 180143,
            pokedexId: 143,
            pokemonName: "Snorlax",
            pokemonImg: "snorlax.png",
            matchups:
            [
                CreateMatchup(180094, "Gengar", "gengar.png", 49.0)
            ]));

        DatabaseInitializer.Initialize(databasePath, [firstSeedDirectory]);
        DatabaseInitializer.Initialize(databasePath, [secondSeedDirectory]);

        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT
    (SELECT COUNT(*) FROM pokemon),
    (SELECT COUNT(*) FROM pokemon_matchup),
    (SELECT COUNT(*) FROM pokemon WHERE name = 'Blastoise'),
    (SELECT COUNT(*) FROM pokemon WHERE name = 'Snorlax');
""";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(0L, reader.GetInt64(2));
        Assert.Equal(1L, reader.GetInt64(3));
    }

    [Fact]
    public void Initialize_IgnoresMissingSeedDirectories()
    {
        var databasePath = Path.Combine(_tempRoot, "db", "unitedrafter.test.db");
        var missingSeedDirectory = Path.Combine(_tempRoot, "missing-seed");

        DatabaseInitializer.Initialize(databasePath, [missingSeedDirectory]);

        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT
    (SELECT COUNT(*) FROM pokemon),
    (SELECT COUNT(*) FROM pokemon_matchup);
""";

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0L, reader.GetInt64(0));
        Assert.Equal(0L, reader.GetInt64(1));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }

    private static void WriteSeedFile(string seedDirectory, string fileName, object payload)
    {
        Directory.CreateDirectory(seedDirectory);
        var path = Path.Combine(seedDirectory, fileName);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }

    private static object CreatePokemonPayload(
        int uniteApiId,
        int pokedexId,
        string pokemonName,
        string pokemonImg,
        object[] matchups)
    {
        return new
        {
            pokemon = new
            {
                id = pokedexId,
                name = new
                {
                    en = pokemonName
                },
                icons = new
                {
                    square = pokemonImg
                }
            },
            counters = new
            {
                pokemonId = uniteApiId,
                all = matchups
            }
        };
    }

    private static object CreateMatchup(int opponentUniteApiId, string opponentName, string opponentImg, double winRate)
    {
        return new
        {
            pokemonId = opponentUniteApiId,
            name = opponentName,
            img = opponentImg,
            winRate
        };
    }
}
