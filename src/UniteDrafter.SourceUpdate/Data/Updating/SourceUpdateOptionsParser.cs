using UniteDrafter.Storage;

namespace UniteDrafter.SourceUpdate.Data.Updating;

public static class SourceUpdateOptionsParser
{
    public static SourceUpdateOptions Parse(string[] args, string? startPath = null)
    {
        string? configuredOutputDirectory = null;
        string? configuredBrowserProfileDirectory = null;
        string? cookieHeader = null;
        string? cookieFile = null;
        var useBrowser = false;
        var headless = false;
        var targets = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--output-dir", StringComparison.OrdinalIgnoreCase))
            {
                configuredOutputDirectory = ReadValue(args, ref i, arg);
                continue;
            }

            if (string.Equals(arg, "--cookie-header", StringComparison.OrdinalIgnoreCase))
            {
                cookieHeader = ReadValue(args, ref i, arg);
                continue;
            }

            if (string.Equals(arg, "--cookie-file", StringComparison.OrdinalIgnoreCase))
            {
                cookieFile = ReadValue(args, ref i, arg);
                continue;
            }

            if (string.Equals(arg, "--browser", StringComparison.OrdinalIgnoreCase))
            {
                useBrowser = true;
                continue;
            }

            if (string.Equals(arg, "--headless", StringComparison.OrdinalIgnoreCase))
            {
                headless = true;
                useBrowser = true;
                continue;
            }

            if (string.Equals(arg, "--profile-dir", StringComparison.OrdinalIgnoreCase))
            {
                configuredBrowserProfileDirectory = ReadValue(args, ref i, arg);
                useBrowser = true;
                continue;
            }

            targets.Add(arg);
        }

        if (!string.IsNullOrWhiteSpace(cookieFile))
        {
            cookieHeader = File.ReadAllText(cookieFile).Trim();
        }

        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            cookieHeader = Environment.GetEnvironmentVariable("UNITE_DRAFTER_COOKIE_HEADER");
        }

        var storageLayout = UniteDrafterStoragePaths.ResolveLayout(startPath ?? AppContext.BaseDirectory);
        var outputDirectory = UniteDrafterStoragePaths.ResolvePathFromRoot(
            storageLayout.RootPath,
            configuredOutputDirectory,
            UniteDrafterStoragePaths.DefaultGuideSourcesDirectory);
        var browserProfileDirectory = UniteDrafterStoragePaths.ResolvePathFromRoot(
            storageLayout.RootPath,
            configuredBrowserProfileDirectory,
            UniteDrafterStoragePaths.DefaultBrowserProfileDirectory);

        return new SourceUpdateOptions(
            outputDirectory,
            targets,
            cookieHeader,
            useBrowser,
            headless,
            browserProfileDirectory,
            storageLayout.SourceUpdateDiagnosticsDirectory);
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }
}
