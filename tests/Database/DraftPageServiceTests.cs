using UniteDrafter.Data;
using UniteDrafter.Services;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class DraftPageServiceTests
{
    [Fact]
    public void SearchPokemon_ReturnsAvailabilityError_WhenDataSourceIsUnavailable()
    {
        var service = new DraftPageService(new FakeDraftPageDataSource
        {
            AvailabilityError = "Database file not found at: test.db"
        });

        var response = service.SearchPokemon("blast");

        Assert.Empty(response.Results);
        Assert.Equal("Database file not found at: test.db", response.ErrorMessage);
    }

    [Fact]
    public void GetPokemonDraftDetails_ReturnsProfileAndSplitMatchups()
    {
        var service = new DraftPageService(new FakeDraftPageDataSource
        {
            Profile = new PokemonProfileResult(180007, 7, "Blastoise", "blastoise.png"),
            Matchups =
            [
                new PokemonMatchupResult("Blastoise", "Gengar", 60.2),
                new PokemonMatchupResult("Blastoise", "Charizard", 52.5),
                new PokemonMatchupResult("Blastoise", "Pikachu", 48.1)
            ]
        });

        var response = service.GetPokemonDraftDetails("Blastoise", matchupLimit: 2);

        Assert.NotNull(response.Details);
        Assert.Null(response.ErrorMessage);
        Assert.Equal("Blastoise", response.Details!.PokemonName);
        Assert.Equal(["Gengar", "Charizard"], response.Details.BestAgainst.Select(x => x.OpponentName).ToArray());
        Assert.Equal(["Pikachu", "Charizard"], response.Details.WorstAgainst.Select(x => x.OpponentName).ToArray());
    }

    [Fact]
    public void GetPokemonDraftDetails_ReturnsValidationError_WhenNameIsBlank()
    {
        var service = new DraftPageService(new FakeDraftPageDataSource());

        var response = service.GetPokemonDraftDetails(" ");

        Assert.Null(response.Details);
        Assert.Equal("Pokemon name is required.", response.ErrorMessage);
    }

    private sealed class FakeDraftPageDataSource : IDraftPageDataSource
    {
        public string? AvailabilityError { get; init; }
        public PokemonProfileResult? Profile { get; init; }
        public IReadOnlyList<PokemonMatchupResult> Matchups { get; init; } = [];
        public IReadOnlyList<PokemonSearchResult> SearchResults { get; init; } = [];

        public string? GetAvailabilityError() => AvailabilityError;

        public IReadOnlyList<PokemonSearchResult> SearchPokemon(string searchTerm, int limit = 8) => SearchResults;

        public PokemonProfileResult? GetPokemonProfile(string pokemonName) => Profile;

        public IReadOnlyList<PokemonMatchupResult> GetMatchupsForPokemon(string pokemonName) => Matchups;
    }
}
