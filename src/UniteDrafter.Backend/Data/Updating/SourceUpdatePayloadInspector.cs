using System.Net;
using System.Text.RegularExpressions;
using DecrypterService = UniteDrafter.Decrypter.Decrypter;

namespace UniteDrafter.Data.Updating;

public static partial class SourceUpdatePayloadInspector
{
    public static string? TryExtractPageJson(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var trimmed = responseText.Trim();
        if (LooksLikeJson(trimmed))
        {
            return trimmed;
        }

        var nextDataMatch = NextDataScriptRegex().Match(responseText);
        if (!nextDataMatch.Success)
        {
            return null;
        }

        var json = WebUtility.HtmlDecode(nextDataMatch.Groups["json"].Value).Trim();
        return LooksLikeJson(json) ? json : null;
    }

    public static void EnsurePayloadContainsCounters(string url, string pageJson)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(pageJson);
        if (PayloadHasCounters(doc.RootElement))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Downloaded payload for {url} but could not find matchup counters in the extracted payload.");
    }

    public static bool PayloadHasCounters(string pageJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(pageJson);
            return PayloadHasCounters(doc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    public static Exception BuildFetchException(string url, string responseText)
    {
        var preview = responseText.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (preview.Length > 220)
        {
            preview = preview[..220] + "...";
        }

        var looksLikeRealGuidePage =
            responseText.Contains("guide: best builds and movesets", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("Statistics", StringComparison.OrdinalIgnoreCase);

        var hasCounterWords =
            responseText.Contains("Counters", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("\"counters\"", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("counter", StringComparison.OrdinalIgnoreCase);

        var cloudflareHint =
            IsLikelyCloudflareChallenge(responseText)
                ? " This looks like a Cloudflare challenge. Pass a real browser cookie header with --cookie-header, --cookie-file, or UNITE_DRAFTER_COOKIE_HEADER."
                : string.Empty;

        var countersHint = looksLikeRealGuidePage
            ? hasCounterWords
                ? " The page loaded, but I could not extract the counters payload format used by this Pokemon."
                : " The page loaded, but no counters payload was found for this Pokemon. UniteAPI may not expose matchup data for it yet."
            : string.Empty;

        return new InvalidOperationException(
            $"Could not extract a usable matchup payload from {url}. Response preview: {preview}{cloudflareHint}{countersHint}");
    }

    public static bool IsLikelyCloudflareChallenge(string responseText)
    {
        return responseText.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("Attention Required", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("/cdn-cgi/", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("challenge-platform", StringComparison.OrdinalIgnoreCase)
            || responseText.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeJson(string text) =>
        text.StartsWith('{') && text.EndsWith('}');

    private static bool PayloadHasCounters(System.Text.Json.JsonElement root)
    {
        var blob = DecrypterService.FindPagePropsE(root);
        if (!string.IsNullOrWhiteSpace(blob))
        {
            try
            {
                var decryptedJson = DecrypterService.DecryptBlob(blob);
                using var decryptedDoc = System.Text.Json.JsonDocument.Parse(decryptedJson);
                return HasCountersNode(decryptedDoc.RootElement);
            }
            catch
            {
                return false;
            }
        }

        return HasCountersNode(root);
    }

    private static bool HasCountersNode(System.Text.Json.JsonElement root)
    {
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("counters", out var counters)
            && counters.ValueKind == System.Text.Json.JsonValueKind.Object
            && counters.TryGetProperty("all", out var allCounters)
            && allCounters.ValueKind == System.Text.Json.JsonValueKind.Array
            && allCounters.GetArrayLength() > 0)
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Object
                && HasCountersNode(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex("""<script[^>]*id=["']__NEXT_DATA__["'][^>]*>\s*(?<json>\{.*?\})\s*</script>""", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex NextDataScriptRegex();
}
