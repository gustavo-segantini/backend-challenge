using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CnabApi.Models;
using CnabApi.Services.Auth;

namespace CnabApi.Controllers;

/// <summary>
/// Authentication endpoints: register/login/refresh and GitHub OAuth callback.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private readonly IAuthService _authService = authService;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { error = result.Error });
        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileResponse>> Me(CancellationToken cancellationToken)
    {
        var result = await _authService.MeAsync(User, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("github/login")]
    [AllowAnonymous]
    public IActionResult GitHubLogin([FromQuery] string? redirectUri = null)
    {
        var url = _authService.BuildGitHubLoginUrl(redirectUri);
        if (string.IsNullOrWhiteSpace(url))
            return StatusCode(500, new { error = "GitHub OAuth is not configured." });
        return Redirect(url);
    }

    [HttpGet("github/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        var result = await _authService.GitHubCallbackAsync(code, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { error = result.Error });

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

    private ActionResult<T> ToActionResult<T>(ServiceResponse<T> result)
    {
        if (result.Success)
            return StatusCode(result.StatusCode, result.Data);

        return StatusCode(result.StatusCode, new { error = result.Error });
    }
}
