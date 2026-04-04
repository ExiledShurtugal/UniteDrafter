using UniteDrafter.Data;
using UniteDrafter.Services;
using UniteDrafter.SourceUpdate.Data;
using UniteDrafter.Storage;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class DraftPageDataSourceFactoryTests : IDisposable
{
    private readonly DatabaseTestHelper helper = new();
    private readonly string? originalStorageRoot =
        Environment.GetEnvironmentVariable(UniteDrafterStoragePaths.StorageRootEnvironmentVariableName);

    public DraftPageDataSourceFactoryTests()
    {
        Environment.SetEnvironmentVariable(UniteDrafterStoragePaths.StorageRootEnvironmentVariableName, null);
    }

    [Fact]
    public void CreateForWorkingDirectory_UsesConfiguredDatabasePath()
    {
        var workingDirectory = helper.CreateSeedDirectory("working-directory");
        Directory.CreateDirectory(workingDirectory);

        var databasePath = helper.CreateDatabasePath("custom.db");
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

        DatabaseRebuilder.RebuildFromSources(databasePath, [seedDirectory]);

        var dataSource = DraftPageDataSourceFactory.CreateForWorkingDirectory(workingDirectory, databasePath);

        Assert.Null(dataSource.GetAvailabilityError());
        Assert.Single(dataSource.SearchPokemon("blast"));
    }

    [Fact]
    public void CreateForWorkingDirectory_UsesDefaultDatabasePath_WhenPathIsNotConfigured()
    {
        var workspaceRoot = helper.CreateSeedDirectory("cwd-root");
        var workingDirectory = Path.Combine(workspaceRoot, "cwd");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "data", "Database"));
        Directory.CreateDirectory(workingDirectory);

        var expectedDatabasePath = UniteDrafterStoragePaths.ResolveLayout(workingDirectory).DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(expectedDatabasePath)!);

        var seedDirectory = helper.CreateSeedDirectory("seed-default");
        helper.WriteSeedFile(seedDirectory, "mew.json", helper.CreatePokemonPayload(
            uniteApiId: 180029,
            pokedexId: 151,
            pokemonName: "Mew",
            pokemonImg: "mew.png",
            matchups:
            [
                helper.CreateMatchup(180007, "Blastoise", "blastoise.png", 51.2)
            ]));

        DatabaseRebuilder.RebuildFromSources(expectedDatabasePath, [seedDirectory]);

        var dataSource = DraftPageDataSourceFactory.CreateForWorkingDirectory(workingDirectory);
        var profile = dataSource.GetPokemonProfile("Mew");

        Assert.Null(dataSource.GetAvailabilityError());
        Assert.NotNull(profile);
        Assert.Equal("Mew", profile!.PokemonName);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            UniteDrafterStoragePaths.StorageRootEnvironmentVariableName,
            originalStorageRoot);
        helper.Dispose();
    }
}
