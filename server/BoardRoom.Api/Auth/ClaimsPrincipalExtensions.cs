using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BoardRoom.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (value is null || !int.TryParse(value, out var id))
        {
            throw new InvalidOperationException("No valid user id claim present on the principal.");
        }

        return id;
    }
}
