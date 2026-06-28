namespace Kotlet.Domain.Auth;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public User User { get; set; } = null!;
    public RefreshToken? ReplacedByToken { get; set; }
}
