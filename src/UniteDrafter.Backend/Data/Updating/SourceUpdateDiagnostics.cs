using System.Text;

namespace UniteDrafter.Data.Updating;

public static class SourceUpdateDiagnostics
{
    private const string FailureDiagnosticsDirectory = "data/Database/Diagnostics/SourceUpdateFailures";

    public static void WriteFailureDiagnostics(string url, string pageContent)
    {
        try
        {
            Directory.CreateDirectory(FailureDiagnosticsDirectory);
            var slug = Path.GetFileName(new Uri(url).AbsolutePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var outputPath = Path.Combine(FailureDiagnosticsDirectory, $"{timestamp}-{slug}.html");
            File.WriteAllText(outputPath, pageContent);
        }
        catch
        {
        }
    }

    internal static void WriteResponseDiagnostics(string url, IReadOnlyList<BrowserResponseCapture> responses)
    {
        if (responses.Count == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(FailureDiagnosticsDirectory);
            var slug = Path.GetFileName(new Uri(url).AbsolutePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var outputPath = Path.Combine(FailureDiagnosticsDirectory, $"{timestamp}-{slug}-responses.txt");

            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            foreach (var response in responses)
            {
                writer.WriteLine($"URL: {response.Url}");
                writer.WriteLine($"Content-Type: {response.ContentType}");
                writer.WriteLine("Body:");
                var shouldTruncateBody =
                    response.Body.Length > 4000
                    && !response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase);

                writer.WriteLine(shouldTruncateBody ? response.Body[..4000] : response.Body);
                writer.WriteLine();
                writer.WriteLine(new string('-', 80));
            }
        }
        catch
        {
        }
    }
}
