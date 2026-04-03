using UniteDrafter.Data;
using UniteDrafter.Data.Updating;

namespace UniteDrafter.Commands;

public static class RefreshDatabaseCommand
{
    public static void Execute(string[] args)
    {
        var refreshArgs = args.Any(arg => string.Equals(arg, "--browser", StringComparison.OrdinalIgnoreCase))
            ? args
            : ["--browser", .. args];

        var options = UpdateSourcesCommand.ParseOptions(refreshArgs);

        Console.WriteLine("Refreshing source files...");
        var updateSummary = UniteApiSourceUpdater.UpdateAsync(options).GetAwaiter().GetResult();

        Console.WriteLine(
            $"Source update complete. Saved: {updateSummary.SavedFiles}, Failed: {updateSummary.FailedFiles}, Output: {updateSummary.OutputDirectory}");

        if (updateSummary.DiscoveredUrls.Count > 0)
        {
            Console.WriteLine($"Guide URLs processed: {updateSummary.DiscoveredUrls.Count}");
        }

        if (updateSummary.Failures.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Continuing with database rebuild using the files that were fetched successfully.");
            Console.WriteLine("Skipped sources:");
            foreach (var failure in updateSummary.Failures)
            {
                Console.WriteLine($"- {failure.Target}: {failure.Error}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Rebuilding database from local source files...");
        DatabaseInitializer.Initialize(jsonSourceDirectories: [options.OutputDirectory]);
    }
}
