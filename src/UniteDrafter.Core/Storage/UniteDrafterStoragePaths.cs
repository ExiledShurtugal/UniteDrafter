namespace UniteDrafter.Storage;

public sealed record UniteDrafterStorageLayout(
    string RootPath,
    string DatabasePath,
    string GuideSourcesDirectory,
    string SourceUpdateDiagnosticsDirectory,
    string BrowserProfileDirectory);

public static class UniteDrafterStoragePaths
{
    public const string StorageRootEnvironmentVariableName = "UNITE_DRAFTER_STORAGE_ROOT";
    public const string InstalledStorageFolderName = "UniteDrafter";

    public static string DefaultDatabasePath { get; } =
        Path.Combine("data", "Database", "unitedrafter.db");

    public static string DefaultGuideSourcesDirectory { get; } =
        Path.Combine("data", "Database", "GuideSources");

    public static string DefaultSourceUpdateDiagnosticsDirectory { get; } =
        Path.Combine("data", "Database", "Diagnostics", "SourceUpdateFailures");

    public static string DefaultBrowserProfileDirectory { get; } =
        Path.Combine(".playwright", "uniteapi-edge-profile");

    public static string DefaultInstalledStorageRoot { get; } =
        ResolveDefaultInstalledStorageRoot();

    public static UniteDrafterStorageLayout ResolveLayout(string startPath, string? configuredStorageRootPath = null)
    {
        var rootPath = ResolveStorageRoot(startPath, configuredStorageRootPath);
        return new UniteDrafterStorageLayout(
            rootPath,
            ResolvePath(rootPath, DefaultDatabasePath),
            ResolvePath(rootPath, DefaultGuideSourcesDirectory),
            ResolvePath(rootPath, DefaultSourceUpdateDiagnosticsDirectory),
            ResolvePath(rootPath, DefaultBrowserProfileDirectory));
    }

    public static string ResolveStorageRoot(string startPath, string? configuredStorageRootPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startPath);

        var normalizedStartPath = Path.GetFullPath(startPath);
        var configuredRoot = string.IsNullOrWhiteSpace(configuredStorageRootPath)
            ? Environment.GetEnvironmentVariable(StorageRootEnvironmentVariableName)
            : configuredStorageRootPath;

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return ResolvePath(normalizedStartPath, configuredRoot);
        }

        var current = new DirectoryInfo(normalizedStartPath);
        while (current is not null)
        {
            if (IsStorageRootCandidate(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return DefaultInstalledStorageRoot;
    }

    public static string ResolveStoragePath(
        string startPath,
        string? configuredStorageRootPath,
        string? configuredPath,
        string defaultRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultRelativePath);
        var rootPath = ResolveStorageRoot(startPath, configuredStorageRootPath);
        return ResolvePathFromRoot(rootPath, configuredPath, defaultRelativePath);
    }

    public static string ResolvePathFromRoot(string rootPath, string? configuredPath, string defaultRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultRelativePath);

        var relativeOrAbsolutePath = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultRelativePath
            : configuredPath;

        return ResolvePath(rootPath, relativeOrAbsolutePath);
    }

    public static string ResolvePath(string basePath, string relativeOrAbsolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeOrAbsolutePath);

        return Path.IsPathRooted(relativeOrAbsolutePath)
            ? Path.GetFullPath(relativeOrAbsolutePath)
            : Path.GetFullPath(Path.Combine(basePath, relativeOrAbsolutePath));
    }

    private static bool IsStorageRootCandidate(string candidatePath)
    {
        return File.Exists(Path.Combine(candidatePath, "UniteDrafter.sln"))
            || Directory.Exists(Path.Combine(candidatePath, ".git"))
            || Directory.Exists(Path.Combine(candidatePath, "data", "Database"));
    }

    private static string ResolveDefaultInstalledStorageRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, InstalledStorageFolderName);
        }

        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(roamingAppData))
        {
            return Path.Combine(roamingAppData, InstalledStorageFolderName);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, ".unitedrafter");
        }

        return Path.Combine(Path.GetTempPath(), InstalledStorageFolderName);
    }
}
