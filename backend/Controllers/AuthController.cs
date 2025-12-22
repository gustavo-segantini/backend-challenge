using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CnabApi.Services.Auth;

namespace CnabApi.Controllers;

/// <summary>
/// Authentication endpoints: register/login/refresh and GitHub OAuth callback.
/// 
/// Handles:
/// - User registration with credentials
/// - Login with username/password or GitHub OAuth
/// - JWT token refresh for expired tokens
/// - Token validation
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
[Tags("Authentication")]
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly ILogger<AuthController> _logger = logger;

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User registration attempt for username: {Username}", request.Username);

        try
        {
            var result = await _authService.RegisterAsync(request, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("User successfully registered: {Username}", request.Username);
            }
            else
            {
                _logger.LogWarning("Registration failed for username: {Username}. Error: {Error}", request.Username, result.Error);
            }

            return ToActionResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration for username: {Username}", request.Username);
            throw;
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login attempt for username: {Username}", request.Username);

        try
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("User successfully logged in: {Username}", request.Username);
            }
            else
            {
                _logger.LogWarning("Login failed for username: {Username}. Error: {Error}", request.Username, result.Error);
            }

            return ToActionResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for username: {Username}", request.Username);
            throw;
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token refresh attempt");

        try
        {
            var result = await _authService.RefreshAsync(request, cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogWarning("Token refresh failed. Error: {Error}", result.Error);
            }
            else
            {
                _logger.LogInformation("Token successfully refreshed");
            }

            return ToActionResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            throw;
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Logout attempt for user: {User}", User?.Identity?.Name ?? "Unknown");

        try
        {
            var result = await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogWarning("Logout failed for user: {User}. Error: {Error}", User?.Identity?.Name ?? "Unknown", result.Error);
                return StatusCode(result.StatusCode, new { error = result.Error });
            }

            _logger.LogInformation("User successfully logged out: {User}", User?.Identity?.Name ?? "Unknown");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during logout for user: {User}", User?.Identity?.Name ?? "Unknown");
            throw;
        }
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> Me(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Profile request for user: {User}", User?.Identity?.Name ?? "Unknown");

            if (User == null)
            {
                _logger.LogWarning("Profile request failed: User is not authenticated");
                return Unauthorized(new { error = "User is not authenticated" });
            }

            var result = await _authService.MeAsync(User, cancellationToken);
            
            if (!result.Success)
            {
                _logger.LogWarning("Profile request failed for user: {User}. Error: {Error}", User?.Identity?.Name ?? "Unknown", result.Error);
            }

            return ToActionResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving profile for user: {User}", User?.Identity?.Name ?? "Unknown");
            throw;
        }
    }

    [HttpGet("github/login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public IActionResult GitHubLogin([FromQuery] string? redirectUri = null)
    {
        _logger.LogInformation("GitHub login initiated. RedirectUri: {RedirectUri}", redirectUri ?? "default");

        try
        {
            var url = _authService.BuildGitHubLoginUrl(redirectUri);
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogError("GitHub OAuth is not configured");
                return StatusCode(500, new { error = "GitHub OAuth is not configured." });
            }

            _logger.LogInformation("Redirecting to GitHub for authentication");
            return Redirect(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GitHub login initialization");
            throw;
        }
    }

    [HttpGet("github/callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GitHub callback received. State: {State}", state ?? "null");

        try
        {
            var result = await _authService.GitHubCallbackAsync(code, cancellationToken);
            if (!result.Success)
            {
                _logger.LogWarning("GitHub callback authentication failed. Error: {Error}", result.Error);
                return StatusCode(result.StatusCode, new { error = result.Error });
            }

            _logger.LogInformation("GitHub authentication successful for user: {Username}", result.Data?.Username ?? "Unknown");

            // If state contains a frontend URL, redirect with tokens in the hash to avoid leaking in logs
            if (!string.IsNullOrWhiteSpace(state))
            {
                var target = state +
                    $"#accessToken={Uri.EscapeDataString(result.Data!.AccessToken)}" +
                    $"&refreshToken={Uri.EscapeDataString(result.Data!.RefreshToken)}" +
                    $"&username={Uri.EscapeDataString(result.Data!.Username)}" +
                    $"&role={Uri.EscapeDataString(result.Data!.Role)}";
                return Redirect(target);
            }

            return Ok(result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GitHub callback processing");
            throw;
        }
    }

    private ActionResult<T> ToActionResult<T>(ServiceResponse<T> result)
    {
        if (result.Success)
            return StatusCode(result.StatusCode, result.Data);

        return StatusCode(result.StatusCode, new { error = result.Error });
    }
}
