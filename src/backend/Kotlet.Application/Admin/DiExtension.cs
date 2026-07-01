using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Admin;

public static class DiExtension
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services) =>
        services.AddScoped<AdminUserService>();
}
