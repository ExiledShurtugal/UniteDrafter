namespace UniteDrafter.Data;

public interface IDatabaseSummaryReader
{
    DatabaseSummary GetDatabaseSummary();
    IReadOnlyList<PokemonMatchupResult> GetMatchupPreview(int limit = 10);
    void PrintDatabaseSummary(int previewLimit = 10);
}
