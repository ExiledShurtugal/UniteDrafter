using UniteDrafter.Storage;

namespace UniteDrafter.Services;

public static class DraftPagePathResolver
{
    public static string ResolveDatabasePath(
        string startPath,
        string? configuredStorageRootPath,
        string? configuredDatabasePath)
    {
        return UniteDrafterStoragePaths.ResolveStoragePath(
            startPath,
            configuredStorageRootPath,
            configuredDatabasePath,
            UniteDrafterStoragePaths.DefaultDatabasePath);
    }
}
