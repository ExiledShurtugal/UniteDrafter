namespace UniteDrafter.Data;

public interface IPokemonMatchupDataReader
{
    IReadOnlyList<PokemonMatchupResult> GetMatchupsForPokemon(string pokemonName);
}
