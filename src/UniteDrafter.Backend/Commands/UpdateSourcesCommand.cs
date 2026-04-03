using UniteDrafter.Data.Updating;

namespace UniteDrafter.Commands;

public static class UpdateSourcesCommand
{
    public static void Execute(string[] args)
    {
        var options = ParseOptions(args);
        var summary = UniteApiSourceUpdater.UpdateAsync(options).GetAwaiter().GetResult();

        Console.WriteLine(
            $"Source update complete. Saved: {summary.SavedFiles}, Failed: {summary.FailedFiles}, Output: {summary.OutputDirectory}");

        if (summary.DiscoveredUrls.Count > 0)
        {
            Console.WriteLine($"Guide URLs processed: {summary.DiscoveredUrls.Count}");
        }

        if (summary.Failures.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Failed downloads:");
        foreach (var failure in summary.Failures)
        {
            Console.WriteLine($"- {failure.Target}: {failure.Error}");
        }
    }

    public static SourceUpdateOptions ParseOptions(string[] args)
    {
        var outputDirectory = "data/Database/GuideSources";
        var browserProfileDirectory = ".playwright/uniteapi-edge-profile";
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
                outputDirectory = ReadValue(args, ref i, arg);
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
                browserProfileDirectory = ReadValue(args, ref i, arg);
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

        return new SourceUpdateOptions(
            outputDirectory,
            targets,
            cookieHeader,
            useBrowser,
            headless,
            browserProfileDirectory);
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
