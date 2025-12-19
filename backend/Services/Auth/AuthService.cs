using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Options;
using System.Text.Json.Serialization;

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

/// <summary>
/// Authentication service for local credentials and GitHub OAuth.
/// </summary>
public class AuthService(
    CnabDbContext db,
    ITokenService tokenService,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    IOptions<GitHubOAuthOptions> gitHubOptions,
    IHttpClientFactory httpClientFactory) : IAuthService
{
    private readonly CnabDbContext _db = db;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly GitHubOAuthOptions _gitHubOptions = gitHubOptions.Value;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<ServiceResponse<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResponse<AuthResponse>.Fail(400, "Username and password are required.");
        }

        var exists = await _db.Users.AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (exists)
        {
            return ServiceResponse<AuthResponse>.Fail(409, "Username already exists.");
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email?.Trim(),
            Role = "User"
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokens.Add(refreshToken);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResponse<AuthResponse>.Ok(BuildAuthResponse(user, refreshToken));
    }

    public async Task<ServiceResponse<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user is null)
            return ServiceResponse<AuthResponse>.Fail(401, "Invalid credentials.");

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return ServiceResponse<AuthResponse>.Fail(401, "Invalid credentials.");

        user.LastLoginAt = DateTime.UtcNow;

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResponse<AuthResponse>.Ok(BuildAuthResponse(user, refreshToken));
    }

    public async Task<ServiceResponse<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ServiceResponse<AuthResponse>.Fail(400, "Refresh token is required.");

        var storedToken = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (storedToken is null || storedToken.User is null || !storedToken.IsActive)
            return ServiceResponse<AuthResponse>.Fail(401, "Invalid or expired refresh token.");

        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByToken = Guid.NewGuid().ToString();

        var newRefresh = _tokenService.GenerateRefreshToken();
        storedToken.User.RefreshTokens.Add(newRefresh);

        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResponse<AuthResponse>.Ok(BuildAuthResponse(storedToken.User, newRefresh));
    }

    public async Task<ServiceResponse<bool>> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var storedToken = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken);

        if (storedToken is null)
            return ServiceResponse<bool>.Fail(404, "Refresh token not found.");

        storedToken.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResponse<bool>.Ok(true);
    }

    public async Task<ServiceResponse<UserProfileResponse>> MeAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var guid))
            return ServiceResponse<UserProfileResponse>.Fail(401, "Unauthorized.");

        var user = await _db.Users.FindAsync(new object?[] { guid }, cancellationToken: cancellationToken);
        if (user is null)
            return ServiceResponse<UserProfileResponse>.Fail(401, "Unauthorized.");

        var profile = new UserProfileResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            GitHubUsername = user.GitHubUsername
        };

        return ServiceResponse<UserProfileResponse>.Ok(profile);
    }

    public string BuildGitHubLoginUrl(string? redirectUri = null)
    {
        if (string.IsNullOrWhiteSpace(_gitHubOptions.ClientId) || string.IsNullOrWhiteSpace(_gitHubOptions.CallbackUrl))
            return string.Empty;

        var callback = _gitHubOptions.CallbackUrl;
        var scope = "read:user user:email";
        var state = string.IsNullOrWhiteSpace(redirectUri) ? null : Uri.EscapeDataString(redirectUri);
        var url = $"https://github.com/login/oauth/authorize?client_id={_gitHubOptions.ClientId}&redirect_uri={Uri.EscapeDataString(callback)}&scope={Uri.EscapeDataString(scope)}";
        if (!string.IsNullOrEmpty(state))
        {
            url += $"&state={state}";
        }
        return url;
    }

    public async Task<ServiceResponse<AuthResponse>> GitHubCallbackAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ServiceResponse<AuthResponse>.Fail(400, "Missing authorization code.");

        if (string.IsNullOrWhiteSpace(_gitHubOptions.ClientId) || string.IsNullOrWhiteSpace(_gitHubOptions.ClientSecret) || string.IsNullOrWhiteSpace(_gitHubOptions.CallbackUrl))
            return ServiceResponse<AuthResponse>.Fail(500, "GitHub OAuth is not configured.");

        var client = _httpClientFactory.CreateClient("GitHubOAuth");

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", _gitHubOptions.ClientId},
                {"client_secret", _gitHubOptions.ClientSecret},
                {"code", code},
                {"redirect_uri", _gitHubOptions.CallbackUrl}
            })
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var tokenResponse = await client.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
            return ServiceResponse<AuthResponse>.Fail(502, $"GitHub token exchange failed ({tokenResponse.StatusCode}).");

        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<GitHubTokenResponse>(cancellationToken: cancellationToken);
        if (tokenPayload?.AccessToken is null)
        {
            var raw = await tokenResponse.Content.ReadAsStringAsync();
            return ServiceResponse<AuthResponse>.Fail(502, $"GitHub token exchange returned no token. Response: {raw}");
        }

        var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenPayload.AccessToken);
        userRequest.Headers.UserAgent.ParseAdd("cnab-api-auth");
        var userResponse = await client.SendAsync(userRequest);
        if (!userResponse.IsSuccessStatusCode)
            return ServiceResponse<AuthResponse>.Fail(502, "Failed to fetch GitHub user info.");

        var ghUser = await userResponse.Content.ReadFromJsonAsync<GitHubUserResponse>(cancellationToken: cancellationToken);
        if (ghUser is null || ghUser.Id == 0)
            return ServiceResponse<AuthResponse>.Fail(502, "Invalid GitHub user response.");

        var user = await _db.Users.Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.GitHubId == ghUser.Id.ToString(), cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Username = ghUser.Login ?? $"gh_{ghUser.Id}",
                Email = ghUser.Email,
                GitHubId = ghUser.Id.ToString(),
                GitHubUsername = ghUser.Login,
                GitHubAvatarUrl = ghUser.AvatarUrl,
                Role = "User",
                PasswordHash = Guid.NewGuid().ToString("N")
            };
            _db.Users.Add(user);
        }
        else
        {
            user.GitHubUsername = ghUser.Login;
            user.GitHubAvatarUrl = ghUser.AvatarUrl;
            user.Email ??= ghUser.Email;
            user.LastLoginAt = DateTime.UtcNow;
        }

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResponse<AuthResponse>.Ok(BuildAuthResponse(user, refreshToken));
    }

    private AuthResponse BuildAuthResponse(User user, RefreshToken refreshToken)
    {
        var access = _tokenService.GenerateAccessToken(user);
        return new AuthResponse
        {
            AccessToken = access,
            RefreshToken = refreshToken.Token,
            Username = user.Username,
            Role = user.Role
        };
    }

    private sealed class GitHubTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }
    }

    private sealed class GitHubUserResponse
    {
        public long Id { get; set; }
        public string? Login { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
    }
}

public record ServiceResponse<T>(bool Success, T? Data, int StatusCode, string? Error = null)
{
    public static ServiceResponse<T> Ok(T data, int statusCode = 200) => new(true, data, statusCode);
    public static ServiceResponse<T> Fail(int statusCode, string error) => new(false, default, statusCode, error);
}

public record UserProfileResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? GitHubUsername { get; set; }
}
