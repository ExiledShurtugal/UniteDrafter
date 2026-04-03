using Microsoft.AspNetCore.Components;
using UniteDrafter.Data;
using UniteDrafter.Services;

namespace UniteDrafter.Frontend.Components.Pages;

public partial class Home
{
    private const int SlotCount = 5;

    [Inject]
    private IDraftPageService DraftPageService { get; set; } = default!;

    private DraftPageState pageState = default!;

    protected override void OnInitialized()
    {
        pageState = new DraftPageState(DraftPageService);
    }

    private PokemonDraftDetails? ActivePokemon => pageState.ActivePokemon;

    private string SearchTerm => pageState.SearchTerm;
    private string SearchMessage => pageState.SearchMessage;
    private IReadOnlyList<PokemonSearchResult> SearchResults => pageState.SearchResults;

    private void SelectSlot(DraftSlotRef slot) => pageState.SelectSlot(slot);

    private void HandleSearchInput(ChangeEventArgs args) =>
        pageState.UpdateSearchTerm(args.Value?.ToString() ?? string.Empty);

    private void AssignPokemon(string pokemonName) => pageState.AssignPokemon(pokemonName);

    private void ClearActiveSlot() => pageState.ClearActiveSlot();

    private void ResetDraft() => pageState.ResetDraft();

    private bool IsActiveSlot(DraftSlotRef slot) => pageState.IsActiveSlot(slot);

    private string GetSlotClass(DraftSlotRef slot)
    {
        var classes = "draft-slot";

        if (slot.Team == TeamSide.Enemy)
        {
            classes += " opponent-slot";
        }

        if (IsActiveSlot(slot))
        {
            classes += " active-slot";
        }

        return classes;
    }

    private string GetSlotTitle(DraftSlotRef slot)
    {
        return pageState.TryGetDraftedPokemon(slot, out var pokemon)
            ? pokemon!.PokemonName
            : slot.Team == TeamSide.Ally
                ? "Empty ally slot"
                : "Empty enemy slot";
    }

    private string GetSlotHint(DraftSlotRef slot)
    {
        if (pageState.HasDraftedPokemon(slot))
        {
            return IsActiveSlot(slot) ? "Selected for replacement or review" : "Click to inspect or replace";
        }

        return IsActiveSlot(slot) ? "Selected for the next pick" : "Click to target this slot";
    }

    private string GetSelectionSummary()
    {
        var selectedPokemon = ActivePokemon?.PokemonName ?? "None yet";
        return $"Selected slot: {GetTeamLabel(pageState.ActiveSlot.Team)} #{pageState.ActiveSlot.Index} | Current pick: {selectedPokemon}";
    }

    private string GetInfoText()
    {
        return ActivePokemon is null
            ? $"{GetTeamLabel(pageState.ActiveSlot.Team)} #{pageState.ActiveSlot.Index} is empty. Search for a Pokemon to assign it."
            : $"{ActivePokemon.PokemonName} is currently assigned to {GetTeamLabel(pageState.ActiveSlot.Team)} #{pageState.ActiveSlot.Index}.";
    }

    private string GetSlotOwnershipText() =>
        $"{GetTeamLabel(pageState.ActiveSlot.Team)} #{pageState.ActiveSlot.Index}";

    private string GetMatchupEmptyText() =>
        ActivePokemon?.CounterStatusMessage ?? "No data yet.";

    private static string GetPokedexText(PokemonDraftDetails pokemon) =>
        pokemon.PokedexId.HasValue ? $"Pokedex #{pokemon.PokedexId.Value}" : "No Pokedex number available";

    private static string GetSearchResultSubtitle(PokemonSearchResult result) =>
        result.PokedexId.HasValue ? $"Pokedex #{result.PokedexId.Value}" : "Pokemon";

    private static string FormatWinRate(double winRate) => $"{winRate:F1}%";

    private static string GetTeamLabel(TeamSide team) =>
        team == TeamSide.Ally ? "Your Team" : "Opponent Team";
}
