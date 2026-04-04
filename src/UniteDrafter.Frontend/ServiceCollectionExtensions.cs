using UniteDrafter.Services;
using UniteDrafter.Storage;

namespace UniteDrafter.Frontend;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDraftPageServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        DatabaseBootstrapper.EnsureInitialized(
            databasePath: configuration["Database:Path"],
            storageRootPath: configuration["Storage:Root"],
            startPath: environment.ContentRootPath);

        services.AddScoped<IDraftPageService>(_ =>
            DraftPageServiceFactory.Create(
                contentRootPath: environment.ContentRootPath,
                configuredDatabasePath: configuration["Database:Path"],
                configuredStorageRootPath: configuration["Storage:Root"]));

        return services;
    }
}
