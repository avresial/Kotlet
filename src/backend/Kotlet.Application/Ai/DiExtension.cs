using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Ai;

public static class DiExtension
{
    public static IServiceCollection AddAiApplication(this IServiceCollection services) => services
        .AddScoped<UserAiProviderService>()
        .AddScoped<IUserChatClientResolver, UserChatClientResolver>()
        .AddSingleton<IApplicationChatClientResolver, ApplicationChatClientResolver>()
        .AddScoped<AiTranslationService>();
}
