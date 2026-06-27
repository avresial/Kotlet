using System.Security.Claims;

namespace Kotlet.Api.Auth;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? HouseId { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public Guid? HouseId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(KotletClaimTypes.HouseId), out var id) ? id : null;
}

public static class KotletClaimTypes
{
    public const string HouseId = "house_id";
}
