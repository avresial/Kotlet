using Kotlet.Application.Ai;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Ai;

public static class DiExtension
{
    public static IServiceCollection AddAiInfrastructure(this IServiceCollection services) => services
        .AddScoped<IUserAiProviderRepository, UserAiProviderRepository>()
        .AddSingleton<IChatClientFactory, OpenRouterChatClientFactory>();
}
