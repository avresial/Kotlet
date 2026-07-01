using Kotlet.Application.Auth;
using Kotlet.Domain.Auth;
using Microsoft.AspNetCore.Identity;

namespace Kotlet.Infrastructure.Auth;

public sealed class UserPasswordService(IPasswordHasher<User> passwordHasher) : IUserPasswordService
{
    public string Hash(User user, string password) => passwordHasher.HashPassword(user, password);

    public bool Verify(User user, string passwordHash, string password) =>
        passwordHasher.VerifyHashedPassword(user, passwordHash, password) != PasswordVerificationResult.Failed;
}
