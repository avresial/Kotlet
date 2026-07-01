using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Measurements;

public static class DiExtension
{
    public static IServiceCollection AddMeasurementsApplication(this IServiceCollection services) =>
        services.AddSingleton<MeasurementMappingService>();
}
