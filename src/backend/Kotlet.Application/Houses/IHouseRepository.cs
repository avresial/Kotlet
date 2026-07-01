using Kotlet.Domain.Auth;
using Kotlet.Domain.Houses;

namespace Kotlet.Application.Houses;

public interface IHouseRepository
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<House?> GetHouseAsync(Guid houseId, CancellationToken cancellationToken);
    Task<HouseMembership?> GetMembershipAsync(Guid userId, Guid houseId, CancellationToken cancellationToken);
    Task<HouseInvitation?> GetInvitationAsync(Guid invitationId, Guid? houseId, Guid? invitedUserId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid userId, Guid houseId, CancellationToken cancellationToken);
    Task<bool> HasAnyHouseAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> HasInvitationAsync(Guid houseId, Guid invitedUserId, CancellationToken cancellationToken);
    Task<Guid?> GetFirstHouseIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HouseListItem>> ListHousesAsync(Guid userId, CancellationToken cancellationToken);
    Task<HouseDetailResponse?> GetDetailAsync(Guid houseId, Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IncomingInvitationResponse>> ListInvitationsAsync(Guid userId, CancellationToken cancellationToken);
    Task<(string Name, int MemberCount)?> GetHouseSummaryAsync(Guid houseId, CancellationToken cancellationToken);
    void AddHouse(House house);
    void AddMembership(HouseMembership membership);
    void AddInvitation(HouseInvitation invitation);
    void RemoveHouse(House house);
    void RemoveMembership(HouseMembership membership);
    void RemoveInvitation(HouseInvitation invitation);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
