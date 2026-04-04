namespace UniteDrafter.SourceUpdate.Data.Updating;

public static class SourceUpdateFailureRecorder
{
    public static void RecordHttpFailure(string diagnosticsDirectory, string url, string responseText)
    {
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            SourceUpdateDiagnostics.WriteFailureDiagnostics(diagnosticsDirectory, url, responseText);
        }
    }

    internal static async Task RecordBrowserFailureAsync(
        string diagnosticsDirectory,
        string url,
        Microsoft.Playwright.IPage page,
        IReadOnlyList<BrowserResponseCapture> responses)
    {
        SourceUpdateDiagnostics.WriteFailureDiagnostics(diagnosticsDirectory, url, await page.ContentAsync());
        SourceUpdateDiagnostics.WriteResponseDiagnostics(diagnosticsDirectory, url, responses);
    }
}
