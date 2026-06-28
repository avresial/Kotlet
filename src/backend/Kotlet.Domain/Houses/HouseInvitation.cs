using Kotlet.Domain.Auth;

namespace Kotlet.Domain.Houses;

public sealed class HouseInvitation
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid InvitedUserId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public House House { get; set; } = null!;
    public User InvitedUser { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
}
