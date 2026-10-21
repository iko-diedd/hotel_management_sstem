using System.Security.Claims;
using HousekeepingApi.Domain;

namespace HousekeepingApi.Infrastructure;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal p) =>
        int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ApiException(401, "Not authenticated."));

    public static UserRole GetRole(this ClaimsPrincipal p) =>
        Enum.Parse<UserRole>(p.FindFirstValue(ClaimTypes.Role) ?? throw new ApiException(401, "Not authenticated."));
}
