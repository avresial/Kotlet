using System.Security.Claims;

namespace Kotlet.Api.Auth;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? HouseId { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var claim = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public Guid? HouseId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(KotletClaimTypes.HouseId), out var id) ? id : null;
}

public static class KotletClaimTypes
{
    public const string HouseId = "house_id";
}
