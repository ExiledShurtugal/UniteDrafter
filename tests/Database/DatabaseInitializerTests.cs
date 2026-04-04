using UniteDrafter.Data;
using UniteDrafter.SourceUpdate.Data;
using UniteDrafter.Storage;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly DatabaseTestHelper _helper = new();

    [Fact]
    public void RebuildFromSources_CreatesSchemaAndSeedsPokemonAndMatchups()
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

        DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);

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
    public void RebuildFromSources_RecreatesSchemaOnEachRun()
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

        DatabaseRebuilder.RebuildFromSources(databasePath, [firstSeedDirectory]);
        DatabaseRebuilder.RebuildFromSources(databasePath, [secondSeedDirectory]);

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
    public void RebuildFromSources_IgnoresMissingSeedDirectories()
    {
        var databasePath = _helper.CreateDatabasePath();
        var missingSeedDirectory = _helper.CreateSeedDirectory("missing-seed");

        var summary = DatabaseRebuilder.RebuildFromSources(databasePath, [missingSeedDirectory]);

        var databaseSummary = new DatabaseSummaryReader(databasePath).GetDatabaseSummary();

        Assert.Equal(0L, databaseSummary.PokemonCount);
        Assert.Equal(0L, databaseSummary.MatchupCount);
        Assert.Equal([missingSeedDirectory], summary.MissingDirectories);
        Assert.Empty(summary.Failures);
    }

    [Fact]
    public void RebuildFromSources_ReportsMalformedSeedFilesWithoutImportingThem()
    {
        var databasePath = _helper.CreateDatabasePath();
        var seedDirectory = _helper.CreateSeedDirectory("seed");
        Directory.CreateDirectory(seedDirectory);
        File.WriteAllText(Path.Combine(seedDirectory, "broken.json"), "{ this is not valid json");

        var summary = DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);
        var databaseSummary = new DatabaseSummaryReader(databasePath).GetDatabaseSummary();

        Assert.Equal(0, summary.ParsedFiles);
        Assert.Equal(1, summary.SkippedFiles);
        Assert.Single(summary.Failures);
        Assert.EndsWith("broken.json", summary.Failures[0].FilePath);
        Assert.Equal(0L, databaseSummary.PokemonCount);
        Assert.Equal(0L, databaseSummary.MatchupCount);
    }

    [Fact]
    public void RebuildFromSources_ReportsFailureWhenCountersPokemonIdIsMissing()
    {
        var databasePath = _helper.CreateDatabasePath();
        var seedDirectory = _helper.CreateSeedDirectory("seed-invalid-id");

        _helper.WriteSeedFile(seedDirectory, "blastoise.json", new
        {
            pokemon = new
            {
                id = 7,
                name = new
                {
                    en = "Blastoise"
                },
                icons = new
                {
                    square = "blastoise.png"
                }
            },
            counters = new
            {
                all = new object[]
                {
                    _helper.CreateMatchup(180006, "Charizard", "charizard.png", 52.5)
                }
            }
        });

        var summary = DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);
        var databaseSummary = new DatabaseSummaryReader(databasePath).GetDatabaseSummary();

        Assert.Equal(0, summary.ParsedFiles);
        Assert.Equal(1, summary.SkippedFiles);
        Assert.Single(summary.Failures);
        Assert.Contains("counters.pokemonId", summary.Failures[0].Error);
        Assert.Equal(0L, databaseSummary.PokemonCount);
        Assert.Equal(0L, databaseSummary.MatchupCount);
    }

    [Fact]
    public void EnsureInitialized_CreatesEmptySchemaWithoutSeeding()
    {
        var databasePath = _helper.CreateDatabasePath();

        var summary = DatabaseBootstrapper.EnsureInitialized(databasePath);
        var databaseSummary = new DatabaseSummaryReader(databasePath).GetDatabaseSummary();

        Assert.True(File.Exists(databasePath));
        Assert.True(summary.CreatedDatabaseFile);
        Assert.True(summary.CreatedSchema);
        Assert.Equal(0L, databaseSummary.PokemonCount);
        Assert.Equal(0L, databaseSummary.MatchupCount);
    }

    [Fact]
    public void EnsureInitialized_DoesNotDeleteExistingData()
    {
        var databasePath = _helper.CreateDatabasePath();
        var seedDirectory = _helper.CreateSeedDirectory("seed-existing");

        _helper.WriteSeedFile(seedDirectory, "blastoise.json", _helper.CreatePokemonPayload(
            uniteApiId: 180007,
            pokedexId: 7,
            pokemonName: "Blastoise",
            pokemonImg: "blastoise.png",
            matchups:
            [
                _helper.CreateMatchup(180006, "Charizard", "charizard.png", 52.5)
            ]));

        DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);

        var summary = DatabaseBootstrapper.EnsureInitialized(databasePath);
        var databaseSummary = new DatabaseSummaryReader(databasePath).GetDatabaseSummary();

        Assert.False(summary.CreatedDatabaseFile);
        Assert.False(summary.CreatedSchema);
        Assert.Equal(2L, databaseSummary.PokemonCount);
        Assert.Equal(1L, databaseSummary.MatchupCount);
    }

    [Fact]
    public void EnsureInitialized_ThrowsWhenExistingSchemaIsIncompatible()
    {
        var databasePath = _helper.CreateDatabasePath("invalid-schema.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        using (var connection = _helper.OpenConnection(databasePath))
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
CREATE TABLE pokemon (
    uniteapi_id INTEGER PRIMARY KEY,
    name TEXT NOT NULL
);

CREATE TABLE pokemon_matchup (
    pokemon_uniteapi_id INTEGER NOT NULL,
    opponent_uniteapi_id INTEGER NOT NULL,
    PRIMARY KEY (pokemon_uniteapi_id, opponent_uniteapi_id)
);
""";
            command.ExecuteNonQuery();
        }

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DatabaseBootstrapper.EnsureInitialized(databasePath));

        Assert.Contains("incompatible", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pokemon", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _helper.Dispose();
    }
}
