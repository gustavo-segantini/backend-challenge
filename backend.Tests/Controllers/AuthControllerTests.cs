using CnabApi.Controllers;
using CnabApi.Models.Requests;
using CnabApi.Models.Responses;
using CnabApi.Services.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CnabApi.Tests.Controllers;

/// <summary>
/// Unit tests for AuthController endpoints covering local auth and GitHub OAuth flows.
/// </summary>
public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<ILogger<AuthController>> _loggerMock = new();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _controller = new AuthController(_authServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Register_WhenSuccessful_ReturnsStatusCodeWithPayload()
    {
        var response = CreateAuthResponse();
        _authServiceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<AuthResponse>.Ok(response));

        var result = await _controller.Register(new RegisterRequest { Username = "user", Password = "pass" }, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task Register_WhenValidationFails_ReturnsError()
    {
        _authServiceMock
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<AuthResponse>.Fail(400, "Username required"));

        var result = await _controller.Register(new RegisterRequest(), CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
        var errorProperty = objectResult.Value!.GetType().GetProperty("error");
        errorProperty!.GetValue(objectResult.Value).Should().Be("Username required");
    }

    [Fact]
    public async Task Login_WhenSuccessful_ReturnsTokens()
    {
        var response = CreateAuthResponse();
        _authServiceMock
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<AuthResponse>.Ok(response));

        var result = await _controller.Login(new LoginRequest { Username = "user", Password = "pass" }, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        _authServiceMock
            .Setup(s => s.RefreshAsync(It.IsAny<RefreshRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<AuthResponse>.Fail(401, "Invalid or expired refresh token."));

        var result = await _controller.Refresh(new RefreshRequest { RefreshToken = "bad" }, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Logout_WhenSuccessful_ReturnsOk()
    {
        _authServiceMock
            .Setup(s => s.LogoutAsync("good", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<bool>.Ok(true));

        var result = await _controller.Logout(new RefreshRequest { RefreshToken = "good" }, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Logout_WhenFails_ReturnsErrorStatus()
    {
        _authServiceMock
            .Setup(s => s.LogoutAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<bool>.Fail(404, "Refresh token not found."));

        var result = await _controller.Logout(new RefreshRequest { RefreshToken = "missing" }, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
        var errorProperty = objectResult.Value!.GetType().GetProperty("error");
        errorProperty!.GetValue(objectResult.Value).Should().Be("Refresh token not found.");
    }

    [Fact]
    public async Task Me_WhenSuccessful_ReturnsProfile()
    {
        // Arrange: Configure a valid user principal
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "user")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Bearer");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var profile = new UserProfileResponse { Username = "user", Role = "User" };
        _authServiceMock
            .Setup(s => s.MeAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<UserProfileResponse>.Ok(profile));

        var result = await _controller.Me(CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(200);
        objectResult.Value.Should().BeEquivalentTo(profile);
    }

    [Fact]
    public async Task Me_WhenUnauthorized_Returns401()
    {
        // Arrange: User is null (no HttpContext configured), so controller returns Unauthorized directly

        var result = await _controller.Me(CancellationToken.None);

        // Assert: Controller returns UnauthorizedObjectResult when User is null
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(401);
        var errorProperty = unauthorizedResult.Value!.GetType().GetProperty("error");
        errorProperty!.GetValue(unauthorizedResult.Value).Should().Be("User is not authenticated");
    }

    [Fact]
    public void GitHubLogin_WhenConfigured_RedirectsToGitHub()
    {
        _authServiceMock
            .Setup(s => s.BuildGitHubLoginUrl(null))
            .Returns("https://github.com/login/oauth/authorize?client_id=test");

        var result = _controller.GitHubLogin();

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("github.com/login/oauth/authorize");
    }

    [Fact]
    public void GitHubLogin_WhenNotConfigured_Returns500()
    {
        _authServiceMock
            .Setup(s => s.BuildGitHubLoginUrl(null))
            .Returns(string.Empty);

        var result = _controller.GitHubLogin();

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GitHubCallback_WithState_RedirectsWithTokens()
    {
        var response = CreateAuthResponse();
        _authServiceMock
            .Setup(s => s.GitHubCallbackAsync("code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<AuthResponse>.Ok(response));

        var result = await _controller.GitHubCallback("code", "https://frontend/auth", CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("accessToken=");
        redirect.Url.Should().Contain("refreshToken=");
        redirect.Url.Should().Contain("username=");
        redirect.Url.Should().Contain("role=");
    }

    [Fact]
    public async Task GitHubCallback_WithoutState_ReturnsOkPayload()
    {
        var response = CreateAuthResponse();
        _authServiceMock
            .Setup(s => s.GitHubCallbackAsync("code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<AuthResponse>.Ok(response));

        var result = await _controller.GitHubCallback("code", null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GitHubCallback_WhenFails_ReturnsErrorStatus()
    {
        _authServiceMock
            .Setup(s => s.GitHubCallbackAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResponse<AuthResponse>.Fail(502, "GitHub token exchange failed"));

        var result = await _controller.GitHubCallback("bad", null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(502);
        var errorProperty = objectResult.Value!.GetType().GetProperty("error");
        errorProperty!.GetValue(objectResult.Value).Should().Be("GitHub token exchange failed");
    }

    private static AuthResponse CreateAuthResponse() => new()
    {
        AccessToken = "access",
        RefreshToken = "refresh",
        Username = "user",
        Role = "User"
    };
}
