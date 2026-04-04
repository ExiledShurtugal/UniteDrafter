namespace UniteDrafter.Services;

public static class DraftPageServiceFactory
{
    public static IDraftPageService Create(
        string contentRootPath,
        string? configuredDatabasePath,
        string? configuredStorageRootPath = null)
    {
        return new DraftPageService(DraftPageDataSourceFactory.Create(
            contentRootPath,
            configuredDatabasePath,
            configuredStorageRootPath));
    }
}
