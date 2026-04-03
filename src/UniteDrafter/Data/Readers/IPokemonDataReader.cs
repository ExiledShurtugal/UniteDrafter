namespace UniteDrafter.Data;

public interface IPokemonDataReader
{
    IReadOnlyList<PokemonSearchResult> SearchPokemon(string searchTerm, int limit = 8);
    PokemonProfileResult? GetPokemonProfile(string pokemonName);
}
