using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Auth;

public static class DiExtension
{
    public static IServiceCollection AddAuthApplication(this IServiceCollection services) =>
        services.AddScoped<AccountService>();
}
