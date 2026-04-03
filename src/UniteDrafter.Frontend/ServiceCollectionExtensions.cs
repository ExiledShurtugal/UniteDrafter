using UniteDrafter.Data;
using UniteDrafter.Services;

namespace UniteDrafter.Frontend;

public static class ServiceCollectionExtensions
{
    private static readonly string DefaultDatabasePath =
        Path.Combine("..", "..", "data", "Database", "unitedrafter.db");

    public static IServiceCollection AddDraftPageServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Database:Path"];
        var relativePath = string.IsNullOrWhiteSpace(configuredPath)
            ? DefaultDatabasePath
            : configuredPath;
        var databasePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, relativePath));

        services.AddScoped<IPokemonDataReader>(_ => new PokemonDataReader(databasePath));
        services.AddScoped<IPokemonMatchupDataReader>(_ => new PokemonMatchupDataReader(databasePath));
        services.AddScoped<IDraftPageService>(sp => new DraftPageService(
            databasePath,
            sp.GetRequiredService<IPokemonDataReader>(),
            sp.GetRequiredService<IPokemonMatchupDataReader>()));

        return services;
    }
}
