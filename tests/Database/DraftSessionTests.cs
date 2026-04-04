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

    private static PokemonDraftDetails CreatePokemon(string name) =>
        new(
            UniteApiId: 1,
            PokedexId: 25,
            PokemonName: name,
            ImageUrl: $"{name.ToLowerInvariant()}.png",
            BestAgainst: Array.Empty<PokemonMatchupResult>(),
            WorstAgainst: Array.Empty<PokemonMatchupResult>(),
            CounterStatusMessage: null);
}
