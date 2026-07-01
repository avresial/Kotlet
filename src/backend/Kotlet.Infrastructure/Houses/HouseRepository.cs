using Kotlet.Application.Houses;
using Kotlet.Domain.Auth;
using Kotlet.Domain.Houses;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Houses;

public sealed class HouseRepository(KotletDbContext dbContext) : IHouseRepository
{
    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<House?> GetHouseAsync(Guid houseId, CancellationToken cancellationToken) =>
        dbContext.Houses.SingleOrDefaultAsync(house => house.Id == houseId, cancellationToken);

    public Task<HouseMembership?> GetMembershipAsync(Guid userId, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.SingleOrDefaultAsync(
            membership => membership.UserId == userId && membership.HouseId == houseId, cancellationToken);

    public Task<HouseInvitation?> GetInvitationAsync(
        Guid invitationId, Guid? houseId, Guid? invitedUserId, CancellationToken cancellationToken) =>
        dbContext.HouseInvitations.SingleOrDefaultAsync(invitation =>
            invitation.Id == invitationId &&
            (!houseId.HasValue || invitation.HouseId == houseId) &&
            (!invitedUserId.HasValue || invitation.InvitedUserId == invitedUserId), cancellationToken);

    public Task<bool> IsMemberAsync(Guid userId, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.AnyAsync(
            membership => membership.UserId == userId && membership.HouseId == houseId, cancellationToken);

    public Task<bool> HasAnyHouseAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.AnyAsync(membership => membership.UserId == userId, cancellationToken);

    public Task<bool> HasInvitationAsync(Guid houseId, Guid invitedUserId, CancellationToken cancellationToken) =>
        dbContext.HouseInvitations.AnyAsync(
            invitation => invitation.HouseId == houseId && invitation.InvitedUserId == invitedUserId, cancellationToken);

    public Task<Guid?> GetFirstHouseIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.JoinedAtUtc)
            .Select(membership => (Guid?)membership.HouseId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<HouseListItem>> ListHousesAsync(
        Guid userId, CancellationToken cancellationToken) =>
        await dbContext.HouseMemberships.AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.House.Name)
            .Select(membership => new HouseListItem(
                membership.HouseId, membership.House.Name, membership.House.Memberships.Count))
            .ToListAsync(cancellationToken);

    public async Task<HouseDetailResponse?> GetDetailAsync(
        Guid houseId, Guid currentUserId, CancellationToken cancellationToken)
    {
        var house = await dbContext.Houses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == houseId, cancellationToken);
        if (house is null) return null;
        var members = await dbContext.HouseMemberships.AsNoTracking()
            .Where(membership => membership.HouseId == houseId)
            .OrderByDescending(membership => membership.UserId == currentUserId)
            .ThenBy(membership => membership.User.DisplayName ?? membership.User.Email)
            .Select(membership => new HouseMemberResponse(membership.User.Id, membership.User.Email,
                membership.User.DisplayName, membership.User.LastLoginAtUtc, membership.User.Id == currentUserId))
            .ToListAsync(cancellationToken);
        var invitations = await dbContext.HouseInvitations.AsNoTracking()
            .Where(invitation => invitation.HouseId == houseId)
            .OrderBy(invitation => invitation.InvitedUser.DisplayName ?? invitation.InvitedUser.Email)
            .Select(invitation => new PendingInvitationResponse(invitation.Id, invitation.InvitedUser.Email,
                invitation.InvitedUser.DisplayName, invitation.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new(house.Id, house.Name, members, invitations);
    }

    public async Task<IReadOnlyList<IncomingInvitationResponse>> ListInvitationsAsync(
        Guid userId, CancellationToken cancellationToken) =>
        await dbContext.HouseInvitations.AsNoTracking()
            .Where(invitation => invitation.InvitedUserId == userId)
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .Select(invitation => new IncomingInvitationResponse(invitation.Id, invitation.HouseId,
                invitation.House.Name, invitation.InvitedByUser.DisplayName ?? invitation.InvitedByUser.Email,
                invitation.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<(string Name, int MemberCount)?> GetHouseSummaryAsync(
        Guid houseId, CancellationToken cancellationToken)
    {
        var result = await dbContext.Houses.AsNoTracking().Where(house => house.Id == houseId)
            .Select(house => new { house.Name, MemberCount = house.Memberships.Count })
            .SingleOrDefaultAsync(cancellationToken);
        return result is null ? null : (result.Name, result.MemberCount);
    }

    public void AddHouse(House house) => dbContext.Houses.Add(house);
    public void AddMembership(HouseMembership membership) => dbContext.HouseMemberships.Add(membership);
    public void AddInvitation(HouseInvitation invitation) => dbContext.HouseInvitations.Add(invitation);
    public void RemoveHouse(House house) => dbContext.Houses.Remove(house);
    public void RemoveMembership(HouseMembership membership) => dbContext.HouseMemberships.Remove(membership);
    public void RemoveInvitation(HouseInvitation invitation) => dbContext.HouseInvitations.Remove(invitation);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
