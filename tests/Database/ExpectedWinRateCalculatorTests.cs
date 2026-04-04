using UniteDrafter.Data;
using UniteDrafter.Services;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class ExpectedWinRateCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsAverageAgainstSelectedOpponents()
    {
        var blastoise = CreatePokemon(
            "Blastoise",
            [
                new PokemonMatchupResult("Blastoise", "Gengar", 60.2),
                new PokemonMatchupResult("Blastoise", "Charizard", 52.5, OpponentUniteApiId: 180006),
                new PokemonMatchupResult("Blastoise", "Pikachu", 48.1, OpponentUniteApiId: 180025)
            ]);

        PokemonDraftDetails[] opposingPokemon =
        [
            CreatePokemon("Charizard", uniteApiId: 180006),
            CreatePokemon("Pikachu", uniteApiId: 180025)
        ];

        var result = ExpectedWinRateCalculator.Calculate(blastoise, opposingPokemon);

        Assert.NotNull(result.ExpectedWinRate);
        Assert.Equal(50.3, result.ExpectedWinRate!.Value, precision: 1);
        Assert.Equal(2, result.ComparedOpponentCount);
        Assert.Equal(2, result.TotalOpponentCount);
        Assert.Equal(["Charizard", "Pikachu"], result.SelectedOpponentMatchups.Select(x => x.OpponentName).ToArray());
        Assert.Empty(result.MissingOpponentNames);
    }

    [Fact]
    public void Calculate_ReportsMissingOpponentsWhenMatchupsAreUnavailable()
    {
        var blastoise = CreatePokemon(
            "Blastoise",
            [
                new PokemonMatchupResult("Blastoise", "Charizard", 52.5, OpponentUniteApiId: 180006)
            ]);

        PokemonDraftDetails[] opposingPokemon =
        [
            CreatePokemon("Charizard", uniteApiId: 180006),
            CreatePokemon("Mew")
        ];

        var result = ExpectedWinRateCalculator.Calculate(blastoise, opposingPokemon);

        Assert.Equal(52.5, result.ExpectedWinRate);
        Assert.Equal(["Mew"], result.MissingOpponentNames);
        Assert.Contains("Missing: Mew.", result.StatusMessage);
    }

    [Fact]
    public void Calculate_MatchesByOpponentIdWhenNamesRepeat()
    {
        var blastoise = CreatePokemon(
            "Blastoise",
            [
                new PokemonMatchupResult("Blastoise", "Mewtwo", 47.0, OpponentUniteApiId: 150001),
                new PokemonMatchupResult("Blastoise", "Mewtwo", 54.0, OpponentUniteApiId: 150002)
            ]);

        PokemonDraftDetails[] opposingPokemon =
        [
            CreatePokemon("Mewtwo", uniteApiId: 150002)
        ];

        var result = ExpectedWinRateCalculator.Calculate(blastoise, opposingPokemon);

        Assert.NotNull(result.ExpectedWinRate);
        Assert.Equal(54.0, result.ExpectedWinRate!.Value);
        Assert.Equal(150002, result.SelectedOpponentMatchups[0].OpponentUniteApiId);
    }

    [Fact]
    public void Calculate_ReturnsGuidanceWhenNoOpponentsAreSelected()
    {
        var result = ExpectedWinRateCalculator.Calculate(CreatePokemon("Blastoise"), Array.Empty<PokemonDraftDetails>());

        Assert.Null(result.ExpectedWinRate);
        Assert.Equal("Select Pokemon on the opposite team to calculate expected win rate.", result.StatusMessage);
    }

    private static PokemonDraftDetails CreatePokemon(
        string name,
        IReadOnlyList<PokemonMatchupResult>? allMatchups = null,
        int uniteApiId = 1) =>
        new(
            UniteApiId: uniteApiId,
            PokedexId: 25,
            PokemonName: name,
            ImageUrl: $"{name.ToLowerInvariant()}.png",
            AllMatchups: allMatchups ?? [],
            BestAgainst: [],
            WorstAgainst: [],
            CounterStatusMessage: null);
}
