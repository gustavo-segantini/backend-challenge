using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Models.Requests;
using CnabApi.Options;
using CnabApi.Services.Auth;

namespace CnabApi.Tests.Services.Auth;

public class AuthServiceTests : IDisposable
{
    private readonly DbContextOptions<CnabDbContext> _dbContextOptions;
    private readonly CnabDbContext _dbContext;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IPasswordHasher<User>> _passwordHasherMock;
    private readonly Mock<IOptions<JwtOptions>> _jwtOptionsMock;
    private readonly Mock<IOptions<GitHubOAuthOptions>> _gitHubOptionsMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<CnabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CnabDbContext(_dbContextOptions);

        _tokenServiceMock = new Mock<ITokenService>();
        _passwordHasherMock = new Mock<IPasswordHasher<User>>();
        _jwtOptionsMock = new Mock<IOptions<JwtOptions>>();
        _gitHubOptionsMock = new Mock<IOptions<GitHubOAuthOptions>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        _jwtOptionsMock.Setup(x => x.Value).Returns(new JwtOptions
        {
            SigningKey = "this-is-a-very-secure-signing-key-with-32-characters",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 7
        });

        _gitHubOptionsMock.Setup(x => x.Value).Returns(new GitHubOAuthOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            CallbackUrl = "https://localhost/auth/callback"
        });

        _authService = new AuthService(
            _dbContext,
            _tokenServiceMock.Object,
            _passwordHasherMock.Object,
            _jwtOptionsMock.Object,
            _gitHubOptionsMock.Object,
            _httpClientFactoryMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Register Tests

    [Fact]
    public async Task RegisterAsync_WithValidRequest_CreatesUserAndReturnsAuthResponse()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "SecurePassword123!"
        };

        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<User>(), request.Password))
            .Returns("hashed-password");

        var refreshToken = new RefreshToken { Token = "refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(refreshToken);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("newuser", result.Data.Username);
        
        // Verify user was added to database
        var savedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == "newuser");
        Assert.NotNull(savedUser);
    }

    [Fact]
    public async Task RegisterAsync_WithMissingUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "",
            Email = "email@example.com",
            Password = "password"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Username and password are required.", result.Error);
    }

    [Fact]
    public async Task RegisterAsync_WithMissingPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "user",
            Email = "email@example.com",
            Password = ""
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Username and password are required.", result.Error);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingUsername_ReturnsConflict()
    {
        // Arrange
        var existingUser = new User { Id = Guid.NewGuid(), Username = "existinguser", PasswordHash = "hash", Role = "User" };
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Username = "existinguser",
            Password = "password123"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Username already exists.", result.Error);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            Role = "User"
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, user.PasswordHash, "password123"))
            .Returns(PasswordVerificationResult.Success);

        var refreshToken = new RefreshToken { Token = "refresh-token" };
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(refreshToken);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");

        var request = new LoginRequest { Username = "testuser", Password = "password123" };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("testuser", result.Data.Username);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Username = "nonexistent", Password = "password" };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Invalid credentials.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hashed-password",
            Role = "User"
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, user.PasswordHash, "wrongpassword"))
            .Returns(PasswordVerificationResult.Failed);

        var request = new LoginRequest { Username = "testuser", Password = "wrongpassword" };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Invalid credentials.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_UpdatesLastLoginTime()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hashed-password",
            Role = "User",
            LastLoginAt = null
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _passwordHasherMock.Setup(x => x.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(new RefreshToken { Token = "token" });
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");

        var request = new LoginRequest { Username = "testuser", Password = "password" };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.True(result.Success);
        var updatedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(updatedUser?.LastLoginAt);
    }

    #endregion

    #region Refresh Tests

    [Fact]
    public async Task RefreshAsync_WithValidRefreshToken_ReturnsNewAuthResponse()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Username = "testuser", Role = "User", PasswordHash = "hash" };
        var refreshToken = new RefreshToken
        {
            Token = "valid-refresh-token",
            User = user,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = null
        };

        _dbContext.Users.Add(user);
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(new RefreshToken { Token = "new-refresh-token" });
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("new-access-token");

        var request = new RefreshRequest { RefreshToken = "valid-refresh-token" };

        // Act
        var result = await _authService.RefreshAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task RefreshAsync_WithMissingRefreshToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "" };

        // Act
        var result = await _authService.RefreshAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Refresh token is required.", result.Error);
    }

    [Fact]
    public async Task RefreshAsync_WithInvalidRefreshToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshRequest { RefreshToken = "invalid-token" };

        // Act
        var result = await _authService.RefreshAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Invalid or expired refresh token.", result.Error);
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredRefreshToken_ReturnsUnauthorized()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Username = "testuser", Role = "User", PasswordHash = "hash" };
        var expiredToken = new RefreshToken
        {
            Token = "expired-token",
            User = user,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            RevokedAt = null
        };

        _dbContext.Users.Add(user);
        _dbContext.RefreshTokens.Add(expiredToken);
        await _dbContext.SaveChangesAsync();

        var request = new RefreshRequest { RefreshToken = "expired-token" };

        // Act
        var result = await _authService.RefreshAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task LogoutAsync_WithValidRefreshToken_ReturnsSuccess()
    {
        // Arrange
        var refreshToken = new RefreshToken { Token = "valid-token", RevokedAt = null };
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authService.LogoutAsync("valid-token");

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task LogoutAsync_WithInvalidRefreshToken_ReturnsFail()
    {
        // Arrange
        // Act
        var result = await _authService.LogoutAsync("invalid-token");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task LogoutAsync_MarksTokenAsRevoked()
    {
        // Arrange
        var refreshToken = new RefreshToken { Token = "valid-token", RevokedAt = null };
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _authService.LogoutAsync("valid-token");

        // Assert
        var updatedToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == "valid-token");
        Assert.NotNull(updatedToken?.RevokedAt);
    }

    #endregion

    #region Me Tests

    [Fact]
    public async Task MeAsync_WithValidPrincipal_ReturnsUserProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            Role = "Admin",
            GitHubUsername = null,
            PasswordHash = "hash"
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authService.MeAsync(principal);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("testuser", result.Data.Username);
        Assert.Equal("test@example.com", result.Data.Email);
    }

    [Fact]
    public async Task MeAsync_WithMissingUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var claims = new List<Claim>();
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authService.MeAsync(principal);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task MeAsync_WithInvalidUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid-guid")
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authService.MeAsync(principal);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task MeAsync_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _authService.MeAsync(principal);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    #endregion

    #region BuildGitHubLoginUrl Tests

    [Fact]
    public void BuildGitHubLoginUrl_WithValidConfiguration_ReturnsCorrectUrl()
    {
        // Act
        var url = _authService.BuildGitHubLoginUrl();

        // Assert
        Assert.NotEmpty(url);
        Assert.Contains("github.com/login/oauth/authorize", url);
        Assert.Contains("client_id=test-client-id", url);
        Assert.Contains("redirect_uri=https%3A%2F%2Flocalhost%2Fauth%2Fcallback", url);
    }

    [Fact]
    public void BuildGitHubLoginUrl_WithRedirectUri_IncludesState()
    {
        // Arrange
        var redirectUri = "https://myapp.com/callback";

        // Act
        var url = _authService.BuildGitHubLoginUrl(redirectUri);

        // Assert
        Assert.NotEmpty(url);
        Assert.Contains("state=", url);
    }

    [Fact]
    public void BuildGitHubLoginUrl_WithoutRedirectUri_NoState()
    {
        // Act
        var url = _authService.BuildGitHubLoginUrl(null);

        // Assert
        Assert.NotEmpty(url);
        Assert.DoesNotContain("state=", url);
    }

    [Fact]
    public void BuildGitHubLoginUrl_WithMissingClientId_ReturnsEmpty()
    {
        // Arrange
        var gitHubOptions = new GitHubOAuthOptions { ClientId = "", ClientSecret = "secret", CallbackUrl = "url" };
        var gitHubOptionsMock = new Mock<IOptions<GitHubOAuthOptions>>();
        gitHubOptionsMock.Setup(x => x.Value).Returns(gitHubOptions);

        var authService = new AuthService(
            _dbContext,
            _tokenServiceMock.Object,
            _passwordHasherMock.Object,
            _jwtOptionsMock.Object,
            gitHubOptionsMock.Object,
            _httpClientFactoryMock.Object);

        // Act
        var url = authService.BuildGitHubLoginUrl();

        // Assert
        Assert.Empty(url);
    }

    #endregion

    #region GitHubCallbackAsync Tests

    [Fact]
    public async Task GitHubCallbackAsync_WithMissingCode_ReturnsFailure()
    {
        // Act
        var result = await _authService.GitHubCallbackAsync("");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Missing authorization code", result.Error);
    }

    [Fact]
    public async Task GitHubCallbackAsync_WithMissingClientId_ReturnsConfigurationError()
    {
        // Arrange
        _gitHubOptionsMock.Setup(x => x.Value).Returns(new GitHubOAuthOptions
        {
            ClientId = "",
            ClientSecret = "test-secret",
            CallbackUrl = "https://localhost/callback"
        });

        var authService = new AuthService(
            _dbContext,
            _tokenServiceMock.Object,
            _passwordHasherMock.Object,
            _jwtOptionsMock.Object,
            _gitHubOptionsMock.Object,
            _httpClientFactoryMock.Object);

        // Act
        var result = await authService.GitHubCallbackAsync("test-code");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
        Assert.Contains("not configured", result.Error);
    }

    [Fact]
    public async Task GitHubCallbackAsync_WithSuccessfulFlow_CreatesNewUser()
    {
        // Arrange
        var code = "test-code";
        var gitHubAccessToken = "gh_access_token";
        var gitHubUserId = 12345L;
        var gitHubUsername = "octocat";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        // Mock token exchange
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("oauth/access_token")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent($"{{\"access_token\":\"{gitHubAccessToken}\",\"token_type\":\"bearer\"}}")
            });

        // Mock user info fetch
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("api.github.com/user")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent($"{{\"id\":{gitHubUserId},\"login\":\"{gitHubUsername}\",\"email\":\"octo@github.com\",\"avatar_url\":\"https://github.com/avatar.png\"}}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitHubOAuth")).Returns(httpClient);

        var refreshToken = new RefreshToken { Token = "refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(refreshToken);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");

        // Act
        var result = await _authService.GitHubCallbackAsync(code);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(gitHubUsername, result.Data.Username);

        // Verify user was created
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.GitHubId == gitHubUserId.ToString());
        Assert.NotNull(user);
        Assert.Equal(gitHubUsername, user.GitHubUsername);
        Assert.Equal("octo@github.com", user.Email);
    }

    [Fact]
    public async Task GitHubCallbackAsync_WithExistingUser_UpdatesUserInfo()
    {
        // Arrange
        var gitHubUserId = 12345L;
        var existingUser = new User
        {
            Username = "oldusername",
            GitHubId = gitHubUserId.ToString(),
            GitHubUsername = "oldusername",
            Email = "old@example.com",
            Role = "User",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var code = "test-code";
        var gitHubAccessToken = "gh_access_token";
        var newGitHubUsername = "newusername";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        // Mock token exchange
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("oauth/access_token")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent($"{{\"access_token\":\"{gitHubAccessToken}\"}}")
            });

        // Mock user info fetch
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("api.github.com/user")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent($"{{\"id\":{gitHubUserId},\"login\":\"{newGitHubUsername}\",\"email\":\"new@github.com\",\"avatar_url\":\"https://github.com/newavatar.png\"}}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitHubOAuth")).Returns(httpClient);

        var refreshToken = new RefreshToken { Token = "refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(refreshToken);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");

        // Act
        var result = await _authService.GitHubCallbackAsync(code);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);

        // Verify user was updated
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.GitHubId == gitHubUserId.ToString());
        Assert.NotNull(user);
        Assert.Equal(newGitHubUsername, user.GitHubUsername);
        Assert.Equal("old@example.com", user.Email); // Email is not updated if already set (user.Email ??= ghUser.Email)
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public async Task GitHubCallbackAsync_WhenTokenExchangeFails_ReturnsError()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.BadRequest
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitHubOAuth")).Returns(httpClient);

        // Act
        var result = await _authService.GitHubCallbackAsync("test-code");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Contains("token exchange failed", result.Error);
    }

    [Fact]
    public async Task GitHubCallbackAsync_WhenUserInfoFetchFails_ReturnsError()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        // Mock successful token exchange
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("oauth/access_token")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent("{\"access_token\":\"token\"}")
            });

        // Mock failed user info fetch
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.ToString().Contains("api.github.com/user")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.Unauthorized
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitHubOAuth")).Returns(httpClient);

        // Act
        var result = await _authService.GitHubCallbackAsync("test-code");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Contains("user info", result.Error);
    }

    [Fact]
    public async Task GitHubCallbackAsync_WhenTokenResponseMissingAccessToken_ReturnsError()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent("{\"error\":\"invalid_grant\"}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitHubOAuth")).Returns(httpClient);

        // Act
        var result = await _authService.GitHubCallbackAsync("test-code");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(502, result.StatusCode);
        Assert.Contains("no token", result.Error);
    }

    #endregion
}

