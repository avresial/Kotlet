using Kotlet.Domain.Auth;

namespace Kotlet.Domain.Houses;

public sealed class HouseMembership
{
    public Guid UserId { get; set; }
    public Guid HouseId { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public User User { get; set; } = null!;
    public House House { get; set; } = null!;
}
