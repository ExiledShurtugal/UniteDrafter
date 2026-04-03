using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace UniteDrafter.Data.Updating;

public static partial class SourceUpdateTargetResolver
{
    private const string UniteApiOrigin = "https://uniteapi.dev";
    private const string GuidePathPrefix = "/pokemon/best-builds-movesets-and-guide-for-";
    private const string PokemonIndexUrl = $"{UniteApiOrigin}/pokemon";

    public static IReadOnlyList<string> ExtractGuideUrls(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        return GuideUrlRegex()
            .Matches(html)
            .Select(match => match.Groups["url"].Value)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static async Task<IReadOnlyList<string>> ResolveTargetUrlsAsync(
        HttpClient httpClient,
        IReadOnlyList<string> targets,
        CancellationToken cancellationToken)
    {
        if (targets.Count > 0)
        {
            return targets.Select(ToGuideUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var rosterHtml = await httpClient.GetStringAsync(PokemonIndexUrl, cancellationToken);
        var discoveredUrls = ExtractGuideUrls(rosterHtml);
        if (discoveredUrls.Count == 0)
        {
            throw SourceUpdatePayloadInspector.BuildFetchException(PokemonIndexUrl, rosterHtml);
        }

        return discoveredUrls;
    }

    public static async Task<IReadOnlyList<string>> ResolveTargetUrlsWithBrowserAsync(
        IPage page,
        IReadOnlyList<string> targets,
        Func<IPage, string, Func<Task<bool>>, string, CancellationToken, Task<string>> getUsableHtmlAsync,
        CancellationToken cancellationToken)
    {
        if (targets.Count > 0)
        {
            return targets.Select(ToGuideUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var rosterHtml = await getUsableHtmlAsync(
            page,
            PokemonIndexUrl,
            async () =>
            {
                var guideUrls = await ExtractGuideUrlsFromPageAsync(page);
                if (guideUrls.Count > 0)
                {
                    return true;
                }

                var names = await ExtractPokemonNamesFromPageAsync(page);
                return names.Count > 0;
            },
            "Waiting for the UniteAPI Pokemon roster page to become usable. If Cloudflare is shown in Edge, complete it there; the updater will keep retrying automatically.",
            cancellationToken);

        var discoveredUrls = await ExtractGuideUrlsFromPageAsync(page);
        if (discoveredUrls.Count == 0)
        {
            var pokemonNames = await ExtractPokemonNamesFromPageAsync(page);
            discoveredUrls = pokemonNames.Select(ConvertPokemonNameToGuideUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (discoveredUrls.Count == 0)
        {
            throw SourceUpdatePayloadInspector.BuildFetchException(PokemonIndexUrl, rosterHtml);
        }

        return discoveredUrls;
    }

    private static string ToGuideUrl(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var slug = target.Trim().Trim('/').ToLowerInvariant();
        if (slug.StartsWith("best-builds-movesets-and-guide-for-", StringComparison.OrdinalIgnoreCase))
        {
            return UniteApiOrigin + "/pokemon/" + slug;
        }

        return UniteApiOrigin + GuidePathPrefix + slug;
    }

    private static async Task<IReadOnlyList<string>> ExtractGuideUrlsFromPageAsync(IPage page)
    {
        var hrefs = await page.EvaluateAsync<string[]>(
            @"() => Array.from(document.querySelectorAll('a[href]'))
                .map(anchor => anchor.href || anchor.getAttribute('href') || '')
                .filter(Boolean)");

        return hrefs
            .Where(href => href.Contains("/pokemon/best-builds-movesets-and-guide-for-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(href => href, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<string>> ExtractPokemonNamesFromPageAsync(IPage page)
    {
        var bodyText = await page.Locator("body").InnerTextAsync();
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return [];
        }

        var blockedLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Unite API",
            "Home",
            "Rankings",
            "Meta",
            "Pokemon",
            "Emblems",
            "About us",
            "Pokemon Unite Pokemons",
            "PokÃ©mon Unite Pokemons",
            "Filters",
            "Melee",
            "Ranged",
            "Physical Attacker",
            "Special Attacker",
            "Attacker",
            "Defender",
            "Speedster",
            "Support",
            "All-Rounder"
        };

        return bodyText.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !blockedLines.Contains(line))
            .Where(line => !line.StartsWith("All assets, images and texts are owned by", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("We are not affiliated", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("This work is licensed", StringComparison.OrdinalIgnoreCase))
            .Where(IsLikelyPokemonName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsLikelyPokemonName(string value)
    {
        if (value.Length is < 3 or > 40)
        {
            return false;
        }

        return value.Any(char.IsLetter) && !value.Contains('%') && !char.IsDigit(value[0]);
    }

    private static string ConvertPokemonNameToGuideUrl(string pokemonName)
    {
        var normalized = pokemonName.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (character == '-')
            {
                builder.Append(character);
            }
        }

        return UniteApiOrigin + GuidePathPrefix + builder;
    }

    [GeneratedRegex("""(?<url>https://uniteapi\.dev/pokemon/best-builds-movesets-and-guide-for-[a-z0-9-]+|/pokemon/best-builds-movesets-and-guide-for-[a-z0-9-]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GuideUrlRegex();
}
