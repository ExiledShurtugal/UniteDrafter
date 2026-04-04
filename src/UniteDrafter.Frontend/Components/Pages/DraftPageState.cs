using UniteDrafter.Data;
using UniteDrafter.Services;

namespace UniteDrafter.Frontend.Components.Pages;

public sealed class DraftPageState
{
    private readonly IDraftPageService draftPageService;
    private readonly DraftSession draftSession = new();
    private readonly Dictionary<int, PokemonDraftDetails> pokemonDetailsCache = [];
    private IReadOnlyList<PokemonSearchResult> allPokemon = [];
    private bool rosterLoadFailed;

    public DraftPageState(IDraftPageService draftPageService)
    {
        this.draftPageService = draftPageService;
        SearchMessage = "Choose a Pokemon for Your Team #1, or filter the roster by name.";
        AvailablePokemon = [];
        AvailablePokemonMessage = "Loading available Pokemon.";
        LoadRoster();
    }

    public DraftSlotRef ActiveSlot => draftSession.ActiveSlot;
    public string SearchTerm { get; private set; } = string.Empty;
    public string SearchMessage { get; private set; }
    public IReadOnlyList<PokemonSearchResult> AvailablePokemon { get; private set; }
    public string AvailablePokemonMessage { get; private set; }

    public PokemonDraftDetails? ActivePokemon => draftSession.ActivePokemon;

    public ExpectedWinRateSummary ActiveExpectedWinRate =>
        ExpectedWinRateCalculator.Calculate(
            draftSession.ActivePokemon,
            draftSession.GetOpposingDraftedPokemon(draftSession.ActiveSlot));

    public void SelectSlot(DraftSlotRef slot)
    {
        draftSession.SelectSlot(slot);
        if (!rosterLoadFailed)
        {
            SetSelectionMessage();
            RefreshAvailablePokemon();
        }
    }

    public void UpdateSearchTerm(string value)
    {
        SearchTerm = value;
        if (!rosterLoadFailed)
        {
            RefreshAvailablePokemon();
        }
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
        pokemonDetailsCache[details.UniteApiId] = details;

        var operation = draftSession.AssignPokemon(details);
        if (operation.Status == DraftSessionOperationStatus.AlreadyDrafted)
        {
            SearchMessage = $"{details.PokemonName} is already drafted.";
            return;
        }

        SearchTerm = string.Empty;
        SearchMessage = $"{details.PokemonName} assigned to {GetTeamLabel(operation.Slot.Team)} #{operation.Slot.Index}.";
        RefreshAvailablePokemon();
    }

    public void ClearActiveSlot()
    {
        var operation = draftSession.ClearActiveSlot();
        if (operation.Status == DraftSessionOperationStatus.Cleared)
        {
            SearchTerm = string.Empty;
            SearchMessage = $"{operation.PokemonName} removed from {GetTeamLabel(operation.Slot.Team)} #{operation.Slot.Index}.";
            RefreshAvailablePokemon();
            return;
        }

        SearchMessage = $"{GetTeamLabel(operation.Slot.Team)} #{operation.Slot.Index} is already empty.";
    }

    public void ResetDraft()
    {
        draftSession.Reset();
        SearchTerm = string.Empty;
        SearchMessage = "Draft reset. Choose a Pokemon for Your Team #1, or filter the roster by name.";
        RefreshAvailablePokemon();
    }

    public bool IsActiveSlot(DraftSlotRef slot) => draftSession.IsActiveSlot(slot);

    public bool TryGetDraftedPokemon(DraftSlotRef slot, out PokemonDraftDetails? pokemon) =>
        draftSession.TryGetDraftedPokemon(slot, out pokemon);

    public bool HasDraftedPokemon(DraftSlotRef slot) => draftSession.HasDraftedPokemon(slot);

    private void LoadRoster()
    {
        var response = draftPageService.GetAllPokemon();
        allPokemon = response.Results;

        if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            rosterLoadFailed = true;
            AvailablePokemon = [];
            AvailablePokemonMessage = response.ErrorMessage;
            SearchMessage = response.ErrorMessage;
            return;
        }

        rosterLoadFailed = false;
        RefreshAvailablePokemon();
    }

    private void RefreshAvailablePokemon()
    {
        var draftablePokemon = allPokemon
            .Where(pokemon => !draftSession.IsDrafted(pokemon.UniteApiId, pokemon.PokemonName))
            .ToArray();

        var trimmedSearchTerm = SearchTerm.Trim();
        var opposingPokemon = draftSession.GetOpposingDraftedPokemon(draftSession.ActiveSlot);

        var filteredPokemon = string.IsNullOrWhiteSpace(trimmedSearchTerm)
            ? draftablePokemon
            : draftablePokemon
                .Where(pokemon => pokemon.PokemonName.Contains(trimmedSearchTerm, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        AvailablePokemon = AvailablePokemonSorter.SortByExpectedWinRate(
            filteredPokemon,
            opposingPokemon,
            GetPokemonDraftDetailsForSorting);

        AvailablePokemonMessage = draftablePokemon.Length == 0
            ? "Every Pokemon in the local roster is already drafted."
            : string.IsNullOrWhiteSpace(trimmedSearchTerm)
                ? BuildAvailablePokemonSummary(
                    $"{draftablePokemon.Length} Pokemon currently available to draft.",
                    opposingPokemon.Count)
                : AvailablePokemon.Count == 0
                    ? $"No available Pokemon match \"{trimmedSearchTerm}\"."
                    : BuildAvailablePokemonSummary(
                        $"Showing {AvailablePokemon.Count} of {draftablePokemon.Length} available Pokemon matching \"{trimmedSearchTerm}\".",
                        opposingPokemon.Count);
    }

    private static string GetTeamLabel(TeamSide team) =>
        team == TeamSide.Ally ? "Your Team" : "Opponent Team";

    private void SetSelectionMessage()
    {
        SearchMessage = ActivePokemon is null
            ? $"Choose a Pokemon for {GetTeamLabel(ActiveSlot.Team)} #{ActiveSlot.Index}, or filter the roster by name."
            : $"{ActivePokemon.PokemonName} is currently assigned to {GetTeamLabel(ActiveSlot.Team)} #{ActiveSlot.Index}.";
    }

    private PokemonDraftDetails? GetPokemonDraftDetailsForSorting(PokemonSearchResult pokemon)
    {
        if (pokemonDetailsCache.TryGetValue(pokemon.UniteApiId, out var cachedDetails))
        {
            return cachedDetails;
        }

        var response = draftPageService.GetPokemonDraftDetails(pokemon.PokemonName);
        if (response.Details is null)
        {
            return null;
        }

        pokemonDetailsCache[pokemon.UniteApiId] = response.Details;
        return response.Details;
    }

    private static string BuildAvailablePokemonSummary(string baseMessage, int opposingPokemonCount)
    {
        return opposingPokemonCount == 0
            ? baseMessage
            : $"{baseMessage} Sorted by expected win rate against selected opponents.";
    }
}
