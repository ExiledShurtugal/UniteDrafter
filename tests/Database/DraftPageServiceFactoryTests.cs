using UniteDrafter.Data;
using UniteDrafter.Services;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class DraftPageServiceFactoryTests : IDisposable
{
    private readonly DatabaseTestHelper helper = new();

    [Fact]
    public void Create_UsesConfiguredDatabasePath()
    {
        var contentRoot = helper.CreateSeedDirectory("content-root");
        Directory.CreateDirectory(contentRoot);

        var databasePath = Path.Combine(contentRoot, "custom.db");
        var seedDirectory = helper.CreateSeedDirectory("seed");
        helper.WriteSeedFile(seedDirectory, "blastoise.json", helper.CreatePokemonPayload(
            uniteApiId: 180007,
            pokedexId: 7,
            pokemonName: "Blastoise",
            pokemonImg: "blastoise.png",
            matchups:
            [
                helper.CreateMatchup(180006, "Charizard", "charizard.png", 52.5)
            ]));

        DatabaseInitializer.Initialize(databasePath, [seedDirectory]);

        var service = DraftPageServiceFactory.Create(contentRoot, "custom.db");
        var response = service.SearchPokemon("blast");

        Assert.Null(response.ErrorMessage);
        Assert.Single(response.Results);
        Assert.Equal("Blastoise", response.Results[0].PokemonName);
    }

    [Fact]
    public void Create_UsesDefaultRelativeDatabasePath_WhenPathIsNotConfigured()
    {
        var contentRoot = Path.Combine(helper.CreateSeedDirectory("site"), "a", "b");
        Directory.CreateDirectory(contentRoot);

        var expectedDatabasePath = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "data", "Database", "unitedrafter.db"));
        Directory.CreateDirectory(Path.GetDirectoryName(expectedDatabasePath)!);

        var seedDirectory = helper.CreateSeedDirectory("seed-default");
        helper.WriteSeedFile(seedDirectory, "blastoise.json", helper.CreatePokemonPayload(
            uniteApiId: 180007,
            pokedexId: 7,
            pokemonName: "Blastoise",
            pokemonImg: "blastoise.png",
            matchups:
            [
                helper.CreateMatchup(180006, "Charizard", "charizard.png", 52.5)
            ]));

        DatabaseInitializer.Initialize(expectedDatabasePath, [seedDirectory]);

        var service = DraftPageServiceFactory.Create(contentRoot, null);
        var response = service.GetPokemonDraftDetails("Blastoise");

        Assert.NotNull(response.Details);
        Assert.Equal("Blastoise", response.Details!.PokemonName);
    }

    public void Dispose() => helper.Dispose();
}
