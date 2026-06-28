using Kotlet.Domain.Houses;

namespace Kotlet.Domain.Auth;

public sealed class User
{
    public Guid Id { get; set; }
    public Guid? DefaultHouseId { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public string? PreferredLanguage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<HouseMembership> Memberships { get; set; } = [];
    public House? DefaultHouse { get; set; }
}
