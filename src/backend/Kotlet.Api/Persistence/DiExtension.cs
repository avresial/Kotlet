namespace Kotlet.Api.Persistence;

public static class DiExtension
{
    public static IServiceCollection AddPersistenceFeature(this IServiceCollection services)
    {
        services.AddSingleton<MigrationReadySignal>();
        services.AddHostedService<DatabaseMigrationWorker>();
        return services;
    }
}
