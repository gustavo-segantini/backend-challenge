using System.Security.Claims;
using CnabApi.Models;

namespace CnabApi.Services.Auth;

public interface IAuthService
{
    Task<ServiceResponse<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResponse<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResponse<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResponse<bool>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<ServiceResponse<UserProfileResponse>> MeAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    string BuildGitHubLoginUrl(string? redirectUri = null);
    Task<ServiceResponse<AuthResponse>> GitHubCallbackAsync(string code, CancellationToken cancellationToken = default);
}
