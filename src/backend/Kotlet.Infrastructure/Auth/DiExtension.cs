using Kotlet.Application.Auth;
using Kotlet.Domain.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Auth;

public static class DiExtension
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services) => services
        .AddScoped<IAuthRepository, AuthRepository>()
        .AddScoped<IAuthSessionRepository, AuthSessionRepository>()
        .AddScoped<IUserPasswordService, UserPasswordService>()
        .AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
}
