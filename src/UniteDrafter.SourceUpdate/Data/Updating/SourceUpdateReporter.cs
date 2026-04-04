namespace UniteDrafter.SourceUpdate.Data.Updating;

public interface ISourceUpdateReporter
{
    void ReportBrowserModeStarted(string browserProfileDirectory);
    void ReportSavedPayload(string outputFileName);
    void ReportChallengePrompt(string prompt);
}

public sealed class NullSourceUpdateReporter : ISourceUpdateReporter
{
    public static NullSourceUpdateReporter Instance { get; } = new();

    private NullSourceUpdateReporter()
    {
    }

    public void ReportBrowserModeStarted(string browserProfileDirectory)
    {
    }

    public void ReportSavedPayload(string outputFileName)
    {
    }

    public void ReportChallengePrompt(string prompt)
    {
    }
}

public sealed class ConsoleSourceUpdateReporter : ISourceUpdateReporter
{
    public void ReportBrowserModeStarted(string browserProfileDirectory)
    {
        Console.WriteLine("Browser mode is active.");
        Console.WriteLine($"Using Edge profile at: {browserProfileDirectory}");
        Console.WriteLine("If Cloudflare appears in the browser, complete the check there and come back to the terminal if prompted.");
    }

    public void ReportSavedPayload(string outputFileName)
    {
        Console.WriteLine($"Saved {outputFileName}");
    }

    public void ReportChallengePrompt(string prompt)
    {
        Console.WriteLine(prompt);
    }
}
