using UniteDrafter.Data;
using UniteDrafter.Services;

namespace UniteDrafter.Frontend.Components.Pages;

public sealed class DraftPageState
{
    private readonly IDraftPageService draftPageService;
    private readonly DraftSession draftSession = new();

    public DraftPageState(IDraftPageService draftPageService)
    {
        this.draftPageService = draftPageService;
        SearchMessage = "Type at least 2 letters to search the database.";
        SearchResults = [];
    }

    public DraftSlotRef ActiveSlot => draftSession.ActiveSlot;
    public string SearchTerm { get; private set; } = string.Empty;
    public string SearchMessage { get; private set; }
    public IReadOnlyList<PokemonSearchResult> SearchResults { get; private set; }

    public PokemonDraftDetails? ActivePokemon => draftSession.ActivePokemon;

    public void SelectSlot(DraftSlotRef slot)
    {
        draftSession.SelectSlot(slot);

        if (ActivePokemon is null)
        {
            SearchMessage = "Type at least 2 letters to search the database.";
        }
    }

    public void UpdateSearchTerm(string value)
    {
        SearchTerm = value;
        RefreshSearchResults();
    }

    public void AssignPokemon(string pokemonName)
    {
        var response = draftPageService.GetPokemonDraftDetails(pokemonName);
        if (response.Details is null)
        {
            SearchMessage = response.ErrorMessage ?? $"Could not load data for \"{pokemonName}\".";
            return;
        }

        var details = response.Details;

        var operation = draftSession.AssignPokemon(details);
        if (operation.Status == DraftSessionOperationStatus.AlreadyDrafted)
        {
            SearchMessage = $"{details.PokemonName} is already drafted.";
            return;
        }

        SearchTerm = details.PokemonName;
        SearchResults = [];
        SearchMessage = $"{details.PokemonName} assigned to {GetTeamLabel(operation.Slot.Team)} #{operation.Slot.Index}.";
    }

    public void ClearActiveSlot()
    {
        var operation = draftSession.ClearActiveSlot();
        if (operation.Status == DraftSessionOperationStatus.Cleared)
        {
            SearchTerm = string.Empty;
            SearchResults = [];
            SearchMessage = $"{operation.PokemonName} removed from {GetTeamLabel(operation.Slot.Team)} #{operation.Slot.Index}.";
            return;
        }

        SearchMessage = $"{GetTeamLabel(operation.Slot.Team)} #{operation.Slot.Index} is already empty.";
    }

    public void ResetDraft()
    {
        draftSession.Reset();
        SearchTerm = string.Empty;
        SearchResults = [];
        SearchMessage = "Draft reset. Type at least 2 letters to search the database.";
    }

    public bool IsActiveSlot(DraftSlotRef slot) => draftSession.IsActiveSlot(slot);

    public bool TryGetDraftedPokemon(DraftSlotRef slot, out PokemonDraftDetails? pokemon) =>
        draftSession.TryGetDraftedPokemon(slot, out pokemon);

    public bool HasDraftedPokemon(DraftSlotRef slot) => draftSession.HasDraftedPokemon(slot);

    private void RefreshSearchResults()
    {
        var term = SearchTerm.Trim();

        if (term.Length < 2)
        {
            SearchResults = [];
            SearchMessage = "Type at least 2 letters to search the database.";
            return;
        }

        var response = draftPageService.SearchPokemon(term);
        SearchResults = response.Results;

        if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            SearchMessage = response.ErrorMessage;
            return;
        }

        SearchMessage = SearchResults.Count == 0
            ? $"No Pokemon found for \"{term}\"."
            : $"Select a Pokemon to place into {GetTeamLabel(ActiveSlot.Team)} #{ActiveSlot.Index}.";
    }

    private static string GetTeamLabel(TeamSide team) =>
        team == TeamSide.Ally ? "Your Team" : "Opponent Team";
}
