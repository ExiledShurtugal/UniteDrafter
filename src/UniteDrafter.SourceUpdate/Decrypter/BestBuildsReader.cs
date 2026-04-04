using System.Globalization;
using System.Text.Json;

namespace UniteDrafter.SourceUpdate.Decrypter;

public sealed record PokemonInfo(
    int UniteApiId,
    int PokedexId,
    string PokemonName,
    string PokemonImg
);

public sealed record PokemonMatchupWinRate(
    int OpponentUniteApiId,
    string OpponentPokemonName,
    string OpponentPokemonImg,
    double WinRate
);

public sealed record PokemonCountersWinRates(
    PokemonInfo Pokemon,
    IReadOnlyDictionary<string, IReadOnlyList<PokemonMatchupWinRate>> CounterSections
);

public static class BestBuildsReader
{
    public static PokemonCountersWinRates ReadPokemonWinRatesFromEncryptedPageFile(string filePath)
    {
        var pageJson = File.ReadAllText(filePath);
        using var pageDoc = JsonDocument.Parse(pageJson);

        var blob = Decrypter.FindPagePropsE(pageDoc.RootElement);
        if (string.IsNullOrWhiteSpace(blob))
        {
            throw new InvalidOperationException("Could not find pageProps.e in input file.");
        }

        var decryptedJson = Decrypter.DecryptBlob(blob);
        using var decryptedDoc = JsonDocument.Parse(decryptedJson);

        return ParsePokemonWinRatesFromDecryptedPayload(decryptedDoc.RootElement);
    }

    public static PokemonCountersWinRates ReadPokemonWinRatesFromDecryptedJsonFile(string filePath)
    {
        var decryptedJson = File.ReadAllText(filePath);
        using var decryptedDoc = JsonDocument.Parse(decryptedJson);

        return ParsePokemonWinRatesFromDecryptedPayload(decryptedDoc.RootElement);
    }

    public static PokemonCountersWinRates ParsePokemonWinRatesFromDecryptedPayload(JsonElement root)
    {
        var pokedexId = ReadInt(root, "pokemon", "id");
        var uniteApiId = ReadInt(root, "counters", "pokemonId");
        if (uniteApiId <= 0)
        {
            throw new InvalidOperationException(
                "Could not read a valid Unite API id from counters.pokemonId.");
        }

        var pokemon = new PokemonInfo(
            uniteApiId,
            pokedexId,
            ReadString(root, "pokemon", "name", "en"),
            ReadString(root, "pokemon", "icons", "square"));

        var counterSections = new Dictionary<string, IReadOnlyList<PokemonMatchupWinRate>>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("counters", out var countersNode) && countersNode.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in countersNode.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                counterSections[property.Name] = ParseCounterSection(property.Value);
            }
        }

        return new PokemonCountersWinRates(pokemon, counterSections);
    }

    private static IReadOnlyList<PokemonMatchupWinRate> ParseCounterSection(JsonElement sectionNode)
    {
        var matchups = new List<PokemonMatchupWinRate>();
        foreach (var matchupNode in sectionNode.EnumerateArray())
        {
            var opponentUniteApiId = ReadInt(matchupNode, "pokemonId");
            var opponentPokemonName = ReadString(matchupNode, "name");
            var opponentPokemonImg = ReadString(matchupNode, "img");
            var winRate = ReadDouble(matchupNode, "winRate");

            if (opponentUniteApiId <= 0 || string.IsNullOrWhiteSpace(opponentPokemonName))
            {
                continue;
            }

            matchups.Add(new PokemonMatchupWinRate(
                opponentUniteApiId,
                opponentPokemonName,
                opponentPokemonImg,
                winRate));
        }

        return matchups;
    }

    private static int ReadInt(JsonElement node, params string[] path)
    {
        var target = Navigate(node, path);
        return target.ValueKind == JsonValueKind.Number && target.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static string ReadString(JsonElement node, params string[] path)
    {
        var target = Navigate(node, path);
        return target.ValueKind == JsonValueKind.String ? (target.GetString() ?? string.Empty) : string.Empty;
    }

    private static double ReadDouble(JsonElement node, params string[] path)
    {
        var target = Navigate(node, path);

        if (target.ValueKind == JsonValueKind.Number && target.TryGetDouble(out var numericValue))
        {
            return numericValue;
        }

        if (target.ValueKind != JsonValueKind.String)
        {
            return 0;
        }

        var value = target.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        value = value.Replace("%", string.Empty).Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static JsonElement Navigate(JsonElement node, params string[] path)
    {
        var current = node;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return default;
            }
        }

        return current;
    }
}
