using UniteDrafter.Data;
using UniteDrafter.Services;

namespace UniteDrafter.Frontend.Components.Pages;

public sealed class DraftPageState
{
    private readonly Dictionary<DraftSlotRef, PokemonDraftDetails> draftedPokemon = [];
    private readonly IDraftPageService draftPageService;

    public DraftPageState(IDraftPageService draftPageService)
    {
        this.draftPageService = draftPageService;
        ActiveSlot = new DraftSlotRef(TeamSide.Ally, 1);
        SearchMessage = "Type at least 2 letters to search the database.";
        SearchResults = [];
    }

    public DraftSlotRef ActiveSlot { get; private set; }
    public string SearchTerm { get; private set; } = string.Empty;
    public string SearchMessage { get; private set; }
    public IReadOnlyList<PokemonSearchResult> SearchResults { get; private set; }

    public PokemonDraftDetails? ActivePokemon =>
        draftedPokemon.TryGetValue(ActiveSlot, out var pokemon) ? pokemon : null;

    public void SelectSlot(DraftSlotRef slot)
    {
        ActiveSlot = slot;

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

        if (HasDuplicateOnSameTeam(details.PokemonName))
        {
            SearchMessage = $"{details.PokemonName} is already on {GetTeamLabel(ActiveSlot.Team)}.";
            return;
        }

        draftedPokemon[ActiveSlot] = details;
        SearchTerm = details.PokemonName;
        SearchResults = [];
        SearchMessage = $"{details.PokemonName} assigned to {GetTeamLabel(ActiveSlot.Team)} #{ActiveSlot.Index}.";
    }

    public bool IsActiveSlot(DraftSlotRef slot) => slot == ActiveSlot;

    public bool TryGetDraftedPokemon(DraftSlotRef slot, out PokemonDraftDetails? pokemon)
    {
        if (draftedPokemon.TryGetValue(slot, out var foundPokemon))
        {
            pokemon = foundPokemon;
            return true;
        }

        pokemon = null;
        return false;
    }

    public bool HasDraftedPokemon(DraftSlotRef slot) => draftedPokemon.ContainsKey(slot);

    private bool HasDuplicateOnSameTeam(string pokemonName)
    {
        foreach (var entry in draftedPokemon)
        {
            if (entry.Key == ActiveSlot)
            {
                continue;
            }

            if (entry.Key.Team != ActiveSlot.Team)
            {
                continue;
            }

            if (string.Equals(entry.Value.PokemonName, pokemonName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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

public enum TeamSide
{
    Ally,
    Enemy
}

public readonly record struct DraftSlotRef(TeamSide Team, int Index);
