using System.Security.Claims;
using CnabApi.Models;

namespace CnabApi.Services.Auth;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    RefreshToken GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
