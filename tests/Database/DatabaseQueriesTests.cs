using UniteDrafter.Data;
using UniteDrafter.SourceUpdate.Data;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class DatabaseQueriesTests : IDisposable
{
    private readonly DatabaseTestHelper _helper = new();

    [Fact]
    public void GetDatabaseSummaryAndMatchupPreview_ReturnExpectedResults()
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

        var summaryReader = new DatabaseSummaryReader(databasePath);
        var summary = summaryReader.GetDatabaseSummary();
        var matchupPreview = summaryReader.GetMatchupPreview(limit: 10);

        Assert.Equal(3L, summary.PokemonCount);
        Assert.Equal(2L, summary.MatchupCount);
        Assert.Contains(matchupPreview, matchup =>
            matchup.PokemonName == "Blastoise"
            && matchup.OpponentName == "Charizard"
            && matchup.WinRate == 52.5);
        Assert.Contains(matchupPreview, matchup =>
            matchup.PokemonName == "Blastoise"
            && matchup.OpponentName == "Pikachu"
            && matchup.WinRate == 48.1);
    }

    [Fact]
    public void GetMatchupsForPokemon_ReturnsAllOpponentsOrderedByWinRate()
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
                _helper.CreateMatchup(180025, "Pikachu", "pikachu.png", 48.1),
                _helper.CreateMatchup(180094, "Gengar", "gengar.png", 60.2)
            ]));

        DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);

        var matchups = new PokemonMatchupDataReader(databasePath).GetMatchupsForPokemon("blastoise");

        Assert.Equal(3, matchups.Count);
        Assert.Equal("Blastoise", matchups[0].PokemonName);
        Assert.Equal("Gengar", matchups[0].OpponentName);
        Assert.Equal(60.2, matchups[0].WinRate);
        Assert.Equal("Charizard", matchups[1].OpponentName);
        Assert.Equal(52.5, matchups[1].WinRate);
        Assert.Equal("Pikachu", matchups[2].OpponentName);
        Assert.Equal(48.1, matchups[2].WinRate);
    }

    [Fact]
    public void GetMatchupsForPokemon_ReturnsEmptyWhenPokemonDoesNotExist()
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
                _helper.CreateMatchup(180006, "Charizard", "charizard.png", 52.5)
            ]));

        DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);

        var matchups = new PokemonMatchupDataReader(databasePath).GetMatchupsForPokemon("Mew");

        Assert.Empty(matchups);
    }

    [Fact]
    public void SearchPokemon_ReturnsPartialMatchesOrderedByRelevance()
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

        _helper.WriteSeedFile(seedDirectory, "blastoise-alt.json", _helper.CreatePokemonPayload(
            uniteApiId: 280007,
            pokedexId: 700,
            pokemonName: "Blasturtle",
            pokemonImg: "blasturtle.png",
            matchups:
            [
                _helper.CreateMatchup(180006, "Charizard", "charizard.png", 44.0)
            ]));

        DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);

        var results = new PokemonDataReader(databasePath).SearchPokemon("blast");

        Assert.Equal(2, results.Count);
        Assert.Equal("Blastoise", results[0].PokemonName);
        Assert.Equal("Blasturtle", results[1].PokemonName);
    }

    [Fact]
    public void GetAllPokemon_ReturnsEntireRosterOrderedByName()
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

        var results = new PokemonDataReader(databasePath).GetAllPokemon();

        Assert.Equal(3, results.Count);
        Assert.Equal(["Blastoise", "Charizard", "Pikachu"], results.Select(x => x.PokemonName).ToArray());
    }

    [Fact]
    public void SearchPokemon_ReturnsEmptyWhenNoPokemonMatch()
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
                _helper.CreateMatchup(180006, "Charizard", "charizard.png", 52.5)
            ]));

        DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);

        var results = new PokemonDataReader(databasePath).SearchPokemon("zzz");

        Assert.Empty(results);
    }

    public void Dispose()
    {
        _helper.Dispose();
    }
}
