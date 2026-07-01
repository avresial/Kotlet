namespace Kotlet.Application.Houses;

public sealed record CreateHouseRequest(string Name);
public sealed record RenameHouseRequest(string Name);
public sealed record InviteMemberRequest(string Email);

public sealed record HouseSummaryResponse(Guid Id, string Name, int MemberCount, bool IsDefault, bool IsActive);
public sealed record HouseMemberResponse(Guid Id, string Email, string? DisplayName, DateTime? LastLoginAtUtc, bool IsCurrentUser);
public sealed record PendingInvitationResponse(Guid Id, string Email, string? DisplayName, DateTime InvitedAtUtc);
public sealed record HouseDetailResponse(Guid Id, string Name, IReadOnlyList<HouseMemberResponse> Members, IReadOnlyList<PendingInvitationResponse> PendingInvitations);
public sealed record IncomingInvitationResponse(Guid Id, Guid HouseId, string HouseName, string InvitedByName, DateTime InvitedAtUtc);

public sealed record HouseListItem(Guid Id, string Name, int MemberCount);

public enum HouseOperationStatus { Success, NotFound, Conflict, ValidationFailed, UserNotFound }

public sealed record HouseOperationResult(
    HouseOperationStatus Status,
    HouseSummaryResponse? House = null,
    PendingInvitationResponse? Invitation = null,
    Guid? ActiveHouseId = null,
    bool ActivateHouse = false,
    Dictionary<string, string[]>? ValidationErrors = null,
    string? Message = null);
