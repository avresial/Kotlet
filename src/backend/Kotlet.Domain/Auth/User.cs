using Kotlet.Domain.Houses;
using Kotlet.Domain.Ai;
using Kotlet.Domain.FoodSettings;

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
    public string Theme { get; set; } = "auto";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<HouseMembership> Memberships { get; set; } = [];
    public House? DefaultHouse { get; set; }
    public UserAiProviderConfiguration? AiProviderConfiguration { get; set; }
    public UserFoodSettings? FoodSettings { get; set; }
}
