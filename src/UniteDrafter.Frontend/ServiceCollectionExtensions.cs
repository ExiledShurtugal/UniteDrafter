using UniteDrafter.Services;

namespace UniteDrafter.Frontend;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDraftPageServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddScoped<IDraftPageService>(_ =>
            DraftPageServiceFactory.Create(
                environment.ContentRootPath,
                configuration["Database:Path"]));

        return services;
    }
}
