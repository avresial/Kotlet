using Kotlet.Domain.Auth;

namespace Kotlet.Domain.Ai;

public sealed class UserAiProviderConfiguration
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string ProviderName { get; set; }
    public required string BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultModel { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public User User { get; set; } = null!;
}
