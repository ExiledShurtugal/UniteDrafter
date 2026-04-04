using UniteDrafter.Data;
using UniteDrafter.Services;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class DraftSessionTests
{
    [Fact]
    public void AssignPokemon_RejectsDuplicateAnywhereInDraft()
    {
        var session = new DraftSession();
        var pikachu = CreatePokemon("Pikachu");

        var firstResult = session.AssignPokemon(pikachu);
        session.SelectSlot(new DraftSlotRef(TeamSide.Enemy, 1));
        var duplicateResult = session.AssignPokemon(pikachu);

        Assert.Equal(DraftSessionOperationStatus.Assigned, firstResult.Status);
        Assert.Equal(DraftSessionOperationStatus.AlreadyDrafted, duplicateResult.Status);
        Assert.False(session.HasDraftedPokemon(new DraftSlotRef(TeamSide.Enemy, 1)));
    }

    [Fact]
    public void ClearActiveSlot_RemovesPokemonFromSelectedSlot()
    {
        var session = new DraftSession();
        session.AssignPokemon(CreatePokemon("Blastoise"));

        var result = session.ClearActiveSlot();

        Assert.Equal(DraftSessionOperationStatus.Cleared, result.Status);
        Assert.Null(session.ActivePokemon);
    }

    [Fact]
    public void Reset_ClearsDraftAndReturnsToDefaultSlot()
    {
        var session = new DraftSession();
        session.AssignPokemon(CreatePokemon("Blastoise"));
        session.SelectSlot(new DraftSlotRef(TeamSide.Enemy, 4));

        var result = session.Reset();

        Assert.Equal(DraftSessionOperationStatus.Reset, result.Status);
        Assert.Equal(new DraftSlotRef(TeamSide.Ally, 1), session.ActiveSlot);
        Assert.Null(session.ActivePokemon);
        Assert.False(session.HasDraftedPokemon(new DraftSlotRef(TeamSide.Ally, 1)));
        Assert.False(session.HasDraftedPokemon(new DraftSlotRef(TeamSide.Enemy, 4)));
    }

    [Fact]
    public void IsDrafted_ReturnsTrueForAssignedPokemon()
    {
        var session = new DraftSession();
        session.AssignPokemon(CreatePokemon("Blastoise", uniteApiId: 180007));

        var result = session.IsDrafted(180007, "Blastoise");

        Assert.True(result);
    }

    [Fact]
    public void GetOpposingDraftedPokemon_ReturnsOppositeTeamOrderedBySlot()
    {
        var session = new DraftSession();
        session.SelectSlot(new DraftSlotRef(TeamSide.Enemy, 3));
        session.AssignPokemon(CreatePokemon("Gengar", uniteApiId: 180094));
        session.SelectSlot(new DraftSlotRef(TeamSide.Enemy, 1));
        session.AssignPokemon(CreatePokemon("Charizard", uniteApiId: 180006));
        session.SelectSlot(new DraftSlotRef(TeamSide.Ally, 2));
        session.AssignPokemon(CreatePokemon("Blastoise", uniteApiId: 180007));

        var opposingPokemon = session.GetOpposingDraftedPokemon(new DraftSlotRef(TeamSide.Ally, 5));

        Assert.Equal(["Charizard", "Gengar"], opposingPokemon.Select(x => x.PokemonName).ToArray());
    }

    private static PokemonDraftDetails CreatePokemon(string name, int uniteApiId = 1) =>
        new(
            UniteApiId: uniteApiId,
            PokedexId: 25,
            PokemonName: name,
            ImageUrl: $"{name.ToLowerInvariant()}.png",
            AllMatchups: Array.Empty<PokemonMatchupResult>(),
            BestAgainst: Array.Empty<PokemonMatchupResult>(),
            WorstAgainst: Array.Empty<PokemonMatchupResult>(),
            CounterStatusMessage: null);
}
