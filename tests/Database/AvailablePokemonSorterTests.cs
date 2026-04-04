using UniteDrafter.Data;
using UniteDrafter.Services;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class AvailablePokemonSorterTests
{
    [Fact]
    public void SortByExpectedWinRate_OrdersCandidatesByHighestAverageFirst()
    {
        PokemonSearchResult[] candidates =
        [
            new PokemonSearchResult(180006, 6, "Charizard"),
            new PokemonSearchResult(180007, 7, "Blastoise"),
            new PokemonSearchResult(180003, 3, "Venusaur")
        ];

        PokemonDraftDetails[] opposingPokemon =
        [
            CreatePokemon("Pikachu", uniteApiId: 180025),
            CreatePokemon("Gengar", uniteApiId: 180094)
        ];

        var detailsByPokemonId = new Dictionary<int, PokemonDraftDetails>
        {
            [180006] = CreatePokemon(
                "Charizard",
                [
                    new PokemonMatchupResult("Charizard", "Pikachu", 48.0, OpponentUniteApiId: 180025),
                    new PokemonMatchupResult("Charizard", "Gengar", 52.0, OpponentUniteApiId: 180094)
                ],
                uniteApiId: 180006),
            [180007] = CreatePokemon(
                "Blastoise",
                [
                    new PokemonMatchupResult("Blastoise", "Pikachu", 58.0, OpponentUniteApiId: 180025),
                    new PokemonMatchupResult("Blastoise", "Gengar", 61.0, OpponentUniteApiId: 180094)
                ],
                uniteApiId: 180007),
            [180003] = CreatePokemon(
                "Venusaur",
                [
                    new PokemonMatchupResult("Venusaur", "Pikachu", 55.0, OpponentUniteApiId: 180025),
                    new PokemonMatchupResult("Venusaur", "Gengar", 57.0, OpponentUniteApiId: 180094)
                ],
                uniteApiId: 180003)
        };

        var sorted = AvailablePokemonSorter.SortByExpectedWinRate(
            candidates,
            opposingPokemon,
            pokemon => detailsByPokemonId[pokemon.UniteApiId]);

        Assert.Equal(["Blastoise", "Venusaur", "Charizard"], sorted.Select(x => x.PokemonName).ToArray());
    }

    [Fact]
    public void SortByExpectedWinRate_PreservesInputOrderWhenNoOpponentsAreSelected()
    {
        var resolveCallCount = 0;
        PokemonSearchResult[] candidates =
        [
            new PokemonSearchResult(180006, 6, "Charizard"),
            new PokemonSearchResult(180007, 7, "Blastoise")
        ];

        var sorted = AvailablePokemonSorter.SortByExpectedWinRate(
            candidates,
            Array.Empty<PokemonDraftDetails>(),
            pokemon =>
            {
                resolveCallCount++;
                return CreatePokemon(pokemon.PokemonName, uniteApiId: pokemon.UniteApiId);
            });

        Assert.Equal(["Charizard", "Blastoise"], sorted.Select(x => x.PokemonName).ToArray());
        Assert.Equal(0, resolveCallCount);
    }

    [Fact]
    public void SortByExpectedWinRate_PushesMissingDataBelowComparableCandidates()
    {
        PokemonSearchResult[] candidates =
        [
            new PokemonSearchResult(180151, 151, "Mew"),
            new PokemonSearchResult(180007, 7, "Blastoise")
        ];

        PokemonDraftDetails[] opposingPokemon =
        [
            CreatePokemon("Charizard", uniteApiId: 180006)
        ];

        var detailsByPokemonId = new Dictionary<int, PokemonDraftDetails?>
        {
            [180151] = CreatePokemon("Mew", [], uniteApiId: 180151),
            [180007] = CreatePokemon(
                "Blastoise",
                [
                    new PokemonMatchupResult("Blastoise", "Charizard", 55.0, OpponentUniteApiId: 180006)
                ],
                uniteApiId: 180007)
        };

        var sorted = AvailablePokemonSorter.SortByExpectedWinRate(
            candidates,
            opposingPokemon,
            pokemon => detailsByPokemonId[pokemon.UniteApiId]);

        Assert.Equal(["Blastoise", "Mew"], sorted.Select(x => x.PokemonName).ToArray());
    }

    private static PokemonDraftDetails CreatePokemon(
        string name,
        IReadOnlyList<PokemonMatchupResult>? allMatchups = null,
        int uniteApiId = 1) =>
        new(
            UniteApiId: uniteApiId,
            PokedexId: null,
            PokemonName: name,
            ImageUrl: $"{name.ToLowerInvariant()}.png",
            AllMatchups: allMatchups ?? [],
            BestAgainst: [],
            WorstAgainst: [],
            CounterStatusMessage: null);
}
