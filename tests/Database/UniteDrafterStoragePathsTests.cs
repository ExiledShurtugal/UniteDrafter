using UniteDrafter.Storage;
using Xunit;

namespace UniteDrafter.Tests.Database;

public sealed class UniteDrafterStoragePathsTests
{
    [Fact]
    public void ResolveLayout_UsesNearestAncestorWithDataDatabaseAsStorageRoot()
    {
        var originalStorageRoot = Environment.GetEnvironmentVariable(
            UniteDrafterStoragePaths.StorageRootEnvironmentVariableName);
        Environment.SetEnvironmentVariable(UniteDrafterStoragePaths.StorageRootEnvironmentVariableName, null);
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UniteDrafter.StoragePathTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var storageRoot = Path.Combine(tempRoot, "workspace");
            var nestedStartPath = Path.Combine(storageRoot, "src", "UniteDrafter.Frontend");
            Directory.CreateDirectory(Path.Combine(storageRoot, "data", "Database"));
            Directory.CreateDirectory(nestedStartPath);

            var layout = UniteDrafterStoragePaths.ResolveLayout(nestedStartPath);

            Assert.Equal(Path.GetFullPath(storageRoot), layout.RootPath);
            Assert.Equal(
                Path.Combine(storageRoot, "data", "Database", "unitedrafter.db"),
                layout.DatabasePath);
            Assert.Equal(
                Path.Combine(storageRoot, "data", "Database", "GuideSources"),
                layout.GuideSourcesDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                UniteDrafterStoragePaths.StorageRootEnvironmentVariableName,
                originalStorageRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveLayout_FallsBackToInstalledStorageRoot_WhenWorkspaceMarkersAreMissing()
    {
        var originalStorageRoot = Environment.GetEnvironmentVariable(
            UniteDrafterStoragePaths.StorageRootEnvironmentVariableName);
        Environment.SetEnvironmentVariable(UniteDrafterStoragePaths.StorageRootEnvironmentVariableName, null);
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UniteDrafter.StoragePathTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var publishedAppPath = Path.Combine(tempRoot, "published-app");
            Directory.CreateDirectory(publishedAppPath);

            var layout = UniteDrafterStoragePaths.ResolveLayout(publishedAppPath);

            Assert.Equal(
                Path.GetFullPath(UniteDrafterStoragePaths.DefaultInstalledStorageRoot),
                layout.RootPath);
            Assert.Equal(
                Path.Combine(layout.RootPath, "data", "Database", "unitedrafter.db"),
                layout.DatabasePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                UniteDrafterStoragePaths.StorageRootEnvironmentVariableName,
                originalStorageRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
