using UniteDrafter.Data;

namespace UniteDrafter.Services;

public static class AvailablePokemonSorter
{
    public static IReadOnlyList<PokemonSearchResult> SortByExpectedWinRate(
        IEnumerable<PokemonSearchResult> candidates,
        IReadOnlyList<PokemonDraftDetails> opposingPokemon,
        Func<PokemonSearchResult, PokemonDraftDetails?> detailsResolver)
    {
        var candidateList = candidates.ToArray();
        if (opposingPokemon.Count == 0)
        {
            return candidateList;
        }

        return candidateList
            .Select(candidate => new CandidateWithSummary(
                candidate,
                ExpectedWinRateCalculator.Calculate(detailsResolver(candidate), opposingPokemon)))
            .OrderByDescending(entry => entry.ExpectedWinRate.ExpectedWinRate.HasValue)
            .ThenByDescending(entry => entry.ExpectedWinRate.ExpectedWinRate ?? double.MinValue)
            .ThenByDescending(entry => entry.ExpectedWinRate.ComparedOpponentCount)
            .ThenBy(entry => entry.Candidate.PokemonName, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Candidate)
            .ToArray();
    }

    private sealed record CandidateWithSummary(
        PokemonSearchResult Candidate,
        ExpectedWinRateSummary ExpectedWinRate);
}
