using UniteDrafter.SourceUpdate.Data.Updating;

namespace UniteDrafter.SourceUpdate.Data;

public sealed record DatabaseRefreshResult(
    SourceUpdateSummary UpdateSummary,
    bool PromotedSnapshot,
    bool RebuiltDatabase,
    string SourceDirectory,
    string? ErrorMessage);

public static class DatabaseRefreshWorkflow
{
    public static Task<DatabaseRefreshResult> RefreshAndRebuildAsync(
        SourceUpdateOptions options,
        ISourceUpdateReporter? reporter = null,
        CancellationToken cancellationToken = default)
    {
        return RefreshAndRebuildAsync(
            options,
            reporter,
            UniteApiSourceUpdater.UpdateAsync,
            outputDirectory => DatabaseRebuilder.RebuildFromSources(jsonSourceDirectories: [outputDirectory]),
            cancellationToken);
    }

    internal static async Task<DatabaseRefreshResult> RefreshAndRebuildAsync(
        SourceUpdateOptions options,
        ISourceUpdateReporter? reporter,
        Func<SourceUpdateOptions, ISourceUpdateReporter?, CancellationToken, Task<SourceUpdateSummary>> updateAsync,
        Action<string> rebuildFromDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(updateAsync);
        ArgumentNullException.ThrowIfNull(rebuildFromDirectory);

        var stagingDirectory = SourceUpdateSnapshotDirectory.CreateStagingDirectory(options.OutputDirectory);

        try
        {
            var stagingOptions = options with
            {
                OutputDirectory = stagingDirectory
            };

            var updateSummary = await updateAsync(stagingOptions, reporter, cancellationToken);
            if (updateSummary.FailedFiles > 0 || updateSummary.Failures.Count > 0)
            {
                return new DatabaseRefreshResult(
                    updateSummary,
                    PromotedSnapshot: false,
                    RebuiltDatabase: false,
                    SourceDirectory: options.OutputDirectory,
                    ErrorMessage:
                        "Source refresh was incomplete. The staged snapshot was discarded and the database was left unchanged.");
            }

            SourceUpdateSnapshotDirectory.PromoteStagedSnapshot(stagingDirectory, options.OutputDirectory);
            rebuildFromDirectory(options.OutputDirectory);

            return new DatabaseRefreshResult(
                updateSummary,
                PromotedSnapshot: true,
                RebuiltDatabase: true,
                SourceDirectory: options.OutputDirectory,
                ErrorMessage: null);
        }
        finally
        {
            SourceUpdateSnapshotDirectory.DeleteDirectoryIfExists(stagingDirectory);
        }
    }
}

internal static class SourceUpdateSnapshotDirectory
{
    public static string CreateStagingDirectory(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var parentDirectory = Path.GetDirectoryName(fullOutputDirectory)
            ?? throw new InvalidOperationException("Could not resolve the parent directory for the source snapshot.");

        Directory.CreateDirectory(parentDirectory);

        var outputDirectoryName = Path.GetFileName(fullOutputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var stagingDirectory = Path.Combine(
            parentDirectory,
            $".{outputDirectoryName}.staging-{Guid.NewGuid():N}");

        Directory.CreateDirectory(stagingDirectory);
        return stagingDirectory;
    }

    public static void PromoteStagedSnapshot(string stagingDirectory, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var fullStagingDirectory = Path.GetFullPath(stagingDirectory);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        if (string.Equals(fullStagingDirectory, fullOutputDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The staging snapshot directory cannot match the live output directory.");
        }

        if (!Directory.Exists(fullStagingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The staged source snapshot does not exist: {fullStagingDirectory}");
        }

        var parentDirectory = Path.GetDirectoryName(fullOutputDirectory)
            ?? throw new InvalidOperationException("Could not resolve the parent directory for the live source snapshot.");
        Directory.CreateDirectory(parentDirectory);

        string? backupDirectory = null;

        try
        {
            if (Directory.Exists(fullOutputDirectory))
            {
                backupDirectory = Path.Combine(
                    parentDirectory,
                    $".{Path.GetFileName(fullOutputDirectory)}.backup-{Guid.NewGuid():N}");
                Directory.Move(fullOutputDirectory, backupDirectory);
            }

            Directory.Move(fullStagingDirectory, fullOutputDirectory);

            if (backupDirectory is not null && Directory.Exists(backupDirectory))
            {
                try
                {
                    Directory.Delete(backupDirectory, recursive: true);
                }
                catch
                {
                    // The live snapshot has already been promoted successfully. A leftover backup is safe to clean up later.
                }
            }
        }
        catch
        {
            if (!Directory.Exists(fullOutputDirectory)
                && backupDirectory is not null
                && Directory.Exists(backupDirectory))
            {
                Directory.Move(backupDirectory, fullOutputDirectory);
            }

            throw;
        }
    }

    public static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
