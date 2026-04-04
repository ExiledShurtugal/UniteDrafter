namespace UniteDrafter.Services;

public enum TeamSide
{
    Ally,
    Enemy
}

public readonly record struct DraftSlotRef(TeamSide Team, int Index);

public enum DraftSessionOperationStatus
{
    Assigned,
    AlreadyDrafted,
    Cleared,
    AlreadyEmpty,
    Reset
}

public sealed record DraftSessionOperationResult(
    DraftSessionOperationStatus Status,
    DraftSlotRef Slot,
    string? PokemonName);

public sealed class DraftSession
{
    private readonly Dictionary<DraftSlotRef, PokemonDraftDetails> draftedPokemon = [];

    public DraftSession()
    {
        ActiveSlot = new DraftSlotRef(TeamSide.Ally, 1);
    }

    public DraftSlotRef ActiveSlot { get; private set; }

    public PokemonDraftDetails? ActivePokemon =>
        draftedPokemon.TryGetValue(ActiveSlot, out var pokemon) ? pokemon : null;

    public void SelectSlot(DraftSlotRef slot)
    {
        ActiveSlot = slot;
    }

    public DraftSessionOperationResult AssignPokemon(PokemonDraftDetails details)
    {
        if (HasDuplicatePick(details))
        {
            return new DraftSessionOperationResult(
                DraftSessionOperationStatus.AlreadyDrafted,
                ActiveSlot,
                details.PokemonName);
        }

        draftedPokemon[ActiveSlot] = details;
        return new DraftSessionOperationResult(
            DraftSessionOperationStatus.Assigned,
            ActiveSlot,
            details.PokemonName);
    }

    public DraftSessionOperationResult ClearActiveSlot()
    {
        if (draftedPokemon.Remove(ActiveSlot, out var removedPokemon))
        {
            return new DraftSessionOperationResult(
                DraftSessionOperationStatus.Cleared,
                ActiveSlot,
                removedPokemon.PokemonName);
        }

        return new DraftSessionOperationResult(
            DraftSessionOperationStatus.AlreadyEmpty,
            ActiveSlot,
            null);
    }

    public DraftSessionOperationResult Reset()
    {
        draftedPokemon.Clear();
        ActiveSlot = new DraftSlotRef(TeamSide.Ally, 1);
        return new DraftSessionOperationResult(
            DraftSessionOperationStatus.Reset,
            ActiveSlot,
            null);
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

    private bool HasDuplicatePick(PokemonDraftDetails details)
    {
        foreach (var entry in draftedPokemon)
        {
            if (entry.Key == ActiveSlot)
            {
                continue;
            }

            if (entry.Value.UniteApiId == details.UniteApiId)
            {
                return true;
            }

            if ((entry.Value.UniteApiId <= 0 || details.UniteApiId <= 0)
                && string.Equals(entry.Value.PokemonName, details.PokemonName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
