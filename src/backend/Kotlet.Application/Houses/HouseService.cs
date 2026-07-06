using Kotlet.Domain.Houses;

namespace Kotlet.Application.Houses;

public sealed class HouseService(IHouseRepository repository)
{
    public async Task<IReadOnlyList<HouseSummaryResponse>> ListAsync(
        Guid userId, Guid? activeHouseId, CancellationToken cancellationToken)
    {
        var user = await repository.GetUserAsync(userId, cancellationToken);
        var houses = await repository.ListHousesAsync(userId, cancellationToken);
        return houses.Select(house => new HouseSummaryResponse(house.Id, house.Name, house.MemberCount,
            house.Id == user?.DefaultHouseId, house.Id == activeHouseId)).ToList();
    }

    public async Task<HouseOperationResult> CreateAsync(
        Guid userId, CreateHouseRequest request, CancellationToken cancellationToken)
    {
        if (ValidateName(request.Name) is { } errors) return Validation(errors);
        var user = await repository.GetUserAsync(userId, cancellationToken);
        if (user is null) return new(HouseOperationStatus.UserNotFound);

        var hadHouse = await repository.HasAnyHouseAsync(userId, cancellationToken);
        var house = new House { Id = Guid.NewGuid(), Name = request.Name.Trim() };
        repository.AddHouse(house);
        repository.AddMembership(new HouseMembership { UserId = userId, HouseId = house.Id, JoinedAtUtc = DateTime.UtcNow });
        if (!hadHouse) user.DefaultHouseId = house.Id;
        await repository.SaveChangesAsync(cancellationToken);

        var summary = new HouseSummaryResponse(house.Id, house.Name, 1, user.DefaultHouseId == house.Id, !hadHouse);
        return new(HouseOperationStatus.Success, summary, ActiveHouseId: house.Id, ActivateHouse: !hadHouse);
    }

    public Task<HouseDetailResponse?> GetAsync(Guid userId, Guid houseId, CancellationToken cancellationToken) =>
        GetForMemberAsync(userId, houseId, cancellationToken);

    public async Task<HouseOperationResult> RenameAsync(
        Guid userId, Guid houseId, RenameHouseRequest request, CancellationToken cancellationToken)
    {
        if (!await repository.IsMemberAsync(userId, houseId, cancellationToken)) return new(HouseOperationStatus.NotFound);
        if (ValidateName(request.Name) is { } errors) return Validation(errors);
        var house = await repository.GetHouseAsync(houseId, cancellationToken);
        if (house is null) return new(HouseOperationStatus.NotFound);
        house.Name = request.Name.Trim();
        await repository.SaveChangesAsync(cancellationToken);
        return new(HouseOperationStatus.Success);
    }

    public async Task<HouseOperationResult> DeleteAsync(Guid userId, Guid houseId, CancellationToken cancellationToken)
    {
        if (!await repository.IsMemberAsync(userId, houseId, cancellationToken)) return new(HouseOperationStatus.NotFound);
        var house = await repository.GetHouseAsync(houseId, cancellationToken);
        if (house is null) return new(HouseOperationStatus.NotFound);
        repository.RemoveHouse(house);
        await repository.SaveChangesAsync(cancellationToken);

        var user = await repository.GetUserAsync(userId, cancellationToken);
        if (user is null) return new(HouseOperationStatus.Success);
        var activeHouseId = user.DefaultHouseId is { } defaultHouseId &&
            await repository.IsMemberAsync(userId, defaultHouseId, cancellationToken)
            ? defaultHouseId
            : await repository.GetFirstHouseIdAsync(userId, cancellationToken);
        return new(HouseOperationStatus.Success, ActiveHouseId: activeHouseId, ActivateHouse: true);
    }

    public async Task<HouseOperationResult> SwitchAsync(Guid userId, Guid houseId, CancellationToken cancellationToken) =>
        await repository.IsMemberAsync(userId, houseId, cancellationToken)
            ? new(HouseOperationStatus.Success, ActiveHouseId: houseId, ActivateHouse: true)
            : new(HouseOperationStatus.NotFound);

    public async Task<HouseOperationResult> InviteAsync(
        Guid userId, Guid houseId, InviteMemberRequest request, CancellationToken cancellationToken)
    {
        if (!await repository.IsMemberAsync(userId, houseId, cancellationToken)) return new(HouseOperationStatus.NotFound);
        var email = request.Email?.Trim() ?? "";
        if (email.Length == 0) return Validation(new() { ["email"] = ["An email is required."] });
        var invitee = await repository.FindUserByNormalizedEmailAsync(email.ToUpperInvariant(), cancellationToken);
        if (invitee is null) return new(HouseOperationStatus.NotFound, Message: "No account exists for this email.");
        if (await repository.IsMemberAsync(invitee.Id, houseId, cancellationToken))
            return new(HouseOperationStatus.Conflict, Message: "This person is already a member.");
        if (await repository.HasInvitationAsync(houseId, invitee.Id, cancellationToken))
            return new(HouseOperationStatus.Conflict, Message: "This person has already been invited.");

        var invitation = new HouseInvitation
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            InvitedUserId = invitee.Id,
            InvitedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };
        repository.AddInvitation(invitation);
        await repository.SaveChangesAsync(cancellationToken);
        return new(HouseOperationStatus.Success, Invitation: new(
            invitation.Id, invitee.Email, invitee.DisplayName, invitation.CreatedAtUtc));
    }

    public async Task<HouseOperationStatus> RemoveMemberAsync(
        Guid userId, Guid houseId, Guid memberUserId, CancellationToken cancellationToken)
    {
        if (!await repository.IsMemberAsync(userId, houseId, cancellationToken)) return HouseOperationStatus.NotFound;
        var membership = await repository.GetMembershipAsync(memberUserId, houseId, cancellationToken);
        if (membership is null) return HouseOperationStatus.NotFound;
        repository.RemoveMembership(membership);
        var removedUser = await repository.GetUserAsync(memberUserId, cancellationToken);
        if (removedUser?.DefaultHouseId == houseId) removedUser.DefaultHouseId = null;
        await repository.SaveChangesAsync(cancellationToken);
        return HouseOperationStatus.Success;
    }

    public async Task<HouseOperationStatus> CancelInvitationAsync(
        Guid userId, Guid houseId, Guid invitationId, CancellationToken cancellationToken)
    {
        if (!await repository.IsMemberAsync(userId, houseId, cancellationToken)) return HouseOperationStatus.NotFound;
        var invitation = await repository.GetInvitationAsync(invitationId, houseId, null, cancellationToken);
        if (invitation is null) return HouseOperationStatus.NotFound;
        repository.RemoveInvitation(invitation);
        await repository.SaveChangesAsync(cancellationToken);
        return HouseOperationStatus.Success;
    }

    public Task<IReadOnlyList<IncomingInvitationResponse>> ListInvitationsAsync(
        Guid userId, CancellationToken cancellationToken) => repository.ListInvitationsAsync(userId, cancellationToken);

    public async Task<HouseOperationResult> AcceptInvitationAsync(
        Guid userId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await repository.GetInvitationAsync(invitationId, null, userId, cancellationToken);
        if (invitation is null) return new(HouseOperationStatus.NotFound);
        var user = await repository.GetUserAsync(userId, cancellationToken);
        if (user is null) return new(HouseOperationStatus.UserNotFound);
        var hadHouse = await repository.HasAnyHouseAsync(userId, cancellationToken);
        var houseId = invitation.HouseId;
        if (!await repository.IsMemberAsync(userId, houseId, cancellationToken))
            repository.AddMembership(new HouseMembership { UserId = userId, HouseId = houseId, JoinedAtUtc = DateTime.UtcNow });
        repository.RemoveInvitation(invitation);
        if (!hadHouse) user.DefaultHouseId = houseId;
        await repository.SaveChangesAsync(cancellationToken);

        var details = await repository.GetHouseSummaryAsync(houseId, cancellationToken);
        var summary = new HouseSummaryResponse(houseId, details?.Name ?? "", details?.MemberCount ?? 0,
            user.DefaultHouseId == houseId, !hadHouse);
        return new(HouseOperationStatus.Success, summary, ActiveHouseId: houseId, ActivateHouse: !hadHouse);
    }

    public async Task<HouseOperationStatus> DeclineInvitationAsync(
        Guid userId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await repository.GetInvitationAsync(invitationId, null, userId, cancellationToken);
        if (invitation is null) return HouseOperationStatus.NotFound;
        repository.RemoveInvitation(invitation);
        await repository.SaveChangesAsync(cancellationToken);
        return HouseOperationStatus.Success;
    }

    private async Task<HouseDetailResponse?> GetForMemberAsync(
        Guid userId, Guid houseId, CancellationToken cancellationToken) =>
        await repository.IsMemberAsync(userId, houseId, cancellationToken)
            ? await repository.GetDetailAsync(houseId, userId, cancellationToken)
            : null;

    private static Dictionary<string, string[]>? ValidateName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? new() { ["name"] = ["A home name is required."] }
        : name.Trim().Length > 150 ? new() { ["name"] = ["Home name cannot exceed 150 characters."] }
        : null;

    private static HouseOperationResult Validation(Dictionary<string, string[]> errors) =>
        new(HouseOperationStatus.ValidationFailed, ValidationErrors: errors);
}
