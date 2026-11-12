using System.Security.Claims;

namespace Sensor.Api.Services;

// Reads the caller's user id from the JWT issued by the Identity service (same approach as Garden and Forum).
public static class CurrentUser
{
    public static Guid? Id(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(id, out var guid) ? guid : null;
    }
}
