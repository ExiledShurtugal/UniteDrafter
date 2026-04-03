using UniteDrafter.Data;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly DatabaseTestHelper _helper = new();

    [Fact]
    public void Initialize_CreatesSchemaAndSeedsPokemonAndMatchups()
    {
        var databasePath = _helper.CreateDatabasePath();
        var seedDirectory = _helper.CreateSeedDirectory("seed");

        _helper.WriteSeedFile(seedDirectory, "blastoise.json", _helper.CreatePokemonPayload(
            uniteApiId: 180007,
            pokedexId: 7,
            pokemonName: "Blastoise",
            pokemonImg: "blastoise.png",
            matchups:
            [
                _helper.CreateMatchup(180006, "Charizard", "charizard.png", 52.5),
                _helper.CreateMatchup(180025, "Pikachu", "pikachu.png", 48.1)
            ]));

        DatabaseInitializer.Initialize(databasePath, [seedDirectory]);

        using var connection = _helper.OpenConnection(databasePath);
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
        var databasePath = _helper.CreateDatabasePath();
        var firstSeedDirectory = _helper.CreateSeedDirectory("seed-first");
        var secondSeedDirectory = _helper.CreateSeedDirectory("seed-second");

        _helper.WriteSeedFile(firstSeedDirectory, "first.json", _helper.CreatePokemonPayload(
            uniteApiId: 180007,
            pokedexId: 7,
            pokemonName: "Blastoise",
            pokemonImg: "blastoise.png",
            matchups:
            [
                _helper.CreateMatchup(180006, "Charizard", "charizard.png", 52.5)
            ]));

        _helper.WriteSeedFile(secondSeedDirectory, "second.json", _helper.CreatePokemonPayload(
            uniteApiId: 180143,
            pokedexId: 143,
            pokemonName: "Snorlax",
            pokemonImg: "snorlax.png",
            matchups:
            [
                _helper.CreateMatchup(180094, "Gengar", "gengar.png", 49.0)
            ]));

        DatabaseInitializer.Initialize(databasePath, [firstSeedDirectory]);
        DatabaseInitializer.Initialize(databasePath, [secondSeedDirectory]);

        using var connection = _helper.OpenConnection(databasePath);
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
        var databasePath = _helper.CreateDatabasePath();
        var missingSeedDirectory = _helper.CreateSeedDirectory("missing-seed");

        DatabaseInitializer.Initialize(databasePath, [missingSeedDirectory]);

        using var connection = _helper.OpenConnection(databasePath);
        var summary = DatabaseQueries.GetDatabaseSummary(connection);

        Assert.Equal(0L, summary.PokemonCount);
        Assert.Equal(0L, summary.MatchupCount);
    }

    public void Dispose()
    {
        _helper.Dispose();
    }
}
