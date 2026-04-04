using UniteDrafter.SourceUpdate.Data.Updating;

namespace UniteDrafter.Commands;

public static class UpdateSourcesCommand
{
    public static void Execute(string[] args)
    {
        var options = ParseOptions(args);
        var summary = UniteApiSourceUpdater.UpdateAsync(options, new ConsoleSourceUpdateReporter()).GetAwaiter().GetResult();

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
        return SourceUpdateOptionsParser.Parse(args);
    }
}
