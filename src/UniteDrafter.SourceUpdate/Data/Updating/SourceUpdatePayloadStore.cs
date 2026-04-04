namespace UniteDrafter.SourceUpdate.Data.Updating;

public static class SourceUpdatePayloadStore
{
    public static async Task<string> SavePayloadAsync(
        string outputDirectory,
        string url,
        string extractedJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(extractedJson);

        Directory.CreateDirectory(outputDirectory);

        var outputFileName = GetOutputFileName(url);
        var outputPath = Path.Combine(outputDirectory, outputFileName);
        await File.WriteAllTextAsync(outputPath, extractedJson, cancellationToken);
        return outputPath;
    }

    public static string GetOutputFileName(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return Path.GetFileName(new Uri(url).AbsolutePath) + ".json";
    }
}
