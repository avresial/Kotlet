using Kotlet.Application.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Admin;

public static class DiExtension
{
    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services) =>
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
}
