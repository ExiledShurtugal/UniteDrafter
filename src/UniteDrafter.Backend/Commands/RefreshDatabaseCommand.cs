using UniteDrafter.SourceUpdate.Data;
using UniteDrafter.SourceUpdate.Data.Updating;

namespace UniteDrafter.Commands;

public static class RefreshDatabaseCommand
{
    public static void Execute(string[] args)
    {
        var refreshArgs = args.Any(arg => string.Equals(arg, "--browser", StringComparison.OrdinalIgnoreCase))
            ? args
            : ["--browser", .. args];

        var options = UpdateSourcesCommand.ParseOptions(refreshArgs);

        Console.WriteLine("Refreshing source files into a staging snapshot...");
        var refreshResult = DatabaseRefreshWorkflow.RefreshAndRebuildAsync(
            options,
            new ConsoleSourceUpdateReporter()).GetAwaiter().GetResult();
        var updateSummary = refreshResult.UpdateSummary;

        Console.WriteLine(
            $"Source update complete. Saved: {updateSummary.SavedFiles}, Failed: {updateSummary.FailedFiles}, Output: {refreshResult.SourceDirectory}");

        if (updateSummary.DiscoveredUrls.Count > 0)
        {
            Console.WriteLine($"Guide URLs processed: {updateSummary.DiscoveredUrls.Count}");
        }

        if (updateSummary.Failures.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(refreshResult.ErrorMessage);
            Console.Error.WriteLine("Skipped sources:");
            foreach (var failure in updateSummary.Failures)
            {
                Console.Error.WriteLine($"- {failure.Target}: {failure.Error}");
            }

            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Database rebuild finished using the refreshed source snapshot.");
    }
}
