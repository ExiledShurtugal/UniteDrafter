using UniteDrafter.Data;

namespace UniteDrafter.Services;

public static class ExpectedWinRateCalculator
{
    public static ExpectedWinRateSummary Calculate(
        PokemonDraftDetails? pokemon,
        IReadOnlyList<PokemonDraftDetails> opposingPokemon)
    {
        if (pokemon is null)
        {
            return new ExpectedWinRateSummary(
                ExpectedWinRate: null,
                ComparedOpponentCount: 0,
                TotalOpponentCount: 0,
                SelectedOpponentMatchups: [],
                MissingOpponentNames: [],
                StatusMessage: "Pick a Pokemon to calculate its expected win rate.");
        }

        if (opposingPokemon.Count == 0)
        {
            return new ExpectedWinRateSummary(
                ExpectedWinRate: null,
                ComparedOpponentCount: 0,
                TotalOpponentCount: 0,
                SelectedOpponentMatchups: [],
                MissingOpponentNames: [],
                StatusMessage: "Select Pokemon on the opposite team to calculate expected win rate.");
        }

        var selectedOpponentMatchups = new List<PokemonMatchupResult>(opposingPokemon.Count);
        var missingOpponentNames = new List<string>();

        foreach (var opponent in opposingPokemon)
        {
            if (TryFindMatchup(pokemon.AllMatchups, opponent, out var matchup))
            {
                selectedOpponentMatchups.Add(matchup!);
            }
            else
            {
                missingOpponentNames.Add(opponent.PokemonName);
            }
        }

        if (selectedOpponentMatchups.Count == 0)
        {
            return new ExpectedWinRateSummary(
                ExpectedWinRate: null,
                ComparedOpponentCount: 0,
                TotalOpponentCount: opposingPokemon.Count,
                SelectedOpponentMatchups: [],
                MissingOpponentNames: missingOpponentNames,
                StatusMessage: BuildMissingDataMessage(opposingPokemon.Count, missingOpponentNames));
        }

        var expectedWinRate = selectedOpponentMatchups.Average(matchup => matchup.WinRate);

        return new ExpectedWinRateSummary(
            ExpectedWinRate: expectedWinRate,
            ComparedOpponentCount: selectedOpponentMatchups.Count,
            TotalOpponentCount: opposingPokemon.Count,
            SelectedOpponentMatchups: selectedOpponentMatchups,
            MissingOpponentNames: missingOpponentNames,
            StatusMessage: BuildSummaryMessage(selectedOpponentMatchups.Count, opposingPokemon.Count, missingOpponentNames));
    }

    private static string BuildMissingDataMessage(int totalOpponentCount, IReadOnlyList<string> missingOpponentNames)
    {
        if (missingOpponentNames.Count == 0)
        {
            return "No matchup data was found for the selected opponents.";
        }

        return $"No matchup data was found for the {FormatOpponentCount(totalOpponentCount)}. Missing: {string.Join(", ", missingOpponentNames)}.";
    }

    private static string BuildSummaryMessage(
        int comparedOpponentCount,
        int totalOpponentCount,
        IReadOnlyList<string> missingOpponentNames)
    {
        if (missingOpponentNames.Count == 0)
        {
            return $"Average across the {FormatOpponentCount(comparedOpponentCount)}.";
        }

        return $"Average across {comparedOpponentCount} of {totalOpponentCount} selected opponents. Missing: {string.Join(", ", missingOpponentNames)}.";
    }

    private static string FormatOpponentCount(int count) =>
        count == 1 ? "1 selected opponent" : $"{count} selected opponents";

    private static bool TryFindMatchup(
        IReadOnlyList<PokemonMatchupResult> allMatchups,
        PokemonDraftDetails opponent,
        out PokemonMatchupResult? matchup)
    {
        if (opponent.UniteApiId > 0)
        {
            matchup = allMatchups.FirstOrDefault(entry => entry.OpponentUniteApiId == opponent.UniteApiId);
            if (matchup is not null)
            {
                return true;
            }
        }

        matchup = allMatchups.FirstOrDefault(entry =>
            string.Equals(entry.OpponentName, opponent.PokemonName, StringComparison.OrdinalIgnoreCase));

        return matchup is not null;
    }
}
