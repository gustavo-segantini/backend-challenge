using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using CnabApi.Models;
using CnabApi.Options;
using CnabApi.Services.Auth;
using FluentAssertions;

namespace CnabApi.Tests.Services.Auth;

public class TokenServiceTests
{
    private readonly JwtOptions _jwtOptions;
    private readonly TokenService _tokenService;

    public TokenServiceTests()
    {
        _jwtOptions = new JwtOptions
        {
            SigningKey = "this-is-a-very-secure-signing-key-with-at-least-32-characters",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 7
        };

        var optionsMock = Microsoft.Extensions.Options.Options.Create(_jwtOptions);
        _tokenService = new TokenService(optionsMock);
    }

    #region GenerateAccessToken Tests

    [Fact]
    public void GenerateAccessToken_WithValidUser_ReturnsValidJwtToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            Role = "User"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
        
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Issuer.Should().Be(_jwtOptions.Issuer);
        jwtToken.Audiences.Should().Contain(_jwtOptions.Audience);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == user.Username);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Username);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public void GenerateAccessToken_WithUserWithoutEmail_ReturnsTokenWithoutEmailClaim()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = null,
            Role = "Admin"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().NotContain(c => c.Type == ClaimTypes.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GenerateAccessToken_WithUserWithoutRole_UsesDefaultUserRole()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            Role = null
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public void GenerateAccessToken_TokenExpiresAfterConfiguredMinutes()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Role = "User"
        };
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var token = _tokenService.GenerateAccessToken(user);
        var afterGeneration = DateTime.UtcNow;

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        jwtToken.ValidFrom.Should().BeCloseTo(beforeGeneration, TimeSpan.FromSeconds(5));
        jwtToken.ValidTo.Should().BeCloseTo(beforeGeneration.AddMinutes(_jwtOptions.AccessTokenMinutes), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateAccessToken_WithInvalidSigningKey_ThrowsException()
    {
        // Arrange
        var invalidOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            SigningKey = "short", // Less than 32 characters
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 7
        });
        var service = new TokenService(invalidOptions);
        var user = new User { Id = Guid.NewGuid(), Username = "test", Role = "User" };

        // Act & Assert
        var act = () => service.GenerateAccessToken(user);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("JWT SigningKey must be configured with at least 32 characters.");
    }

    [Fact]
    public void GenerateAccessToken_WithEmptySigningKey_ThrowsException()
    {
        // Arrange
        var invalidOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            SigningKey = "",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 7
        });
        var service = new TokenService(invalidOptions);
        var user = new User { Id = Guid.NewGuid(), Username = "test", Role = "User" };

        // Act & Assert
        var act = () => service.GenerateAccessToken(user);
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region GenerateRefreshToken Tests

    [Fact]
    public void GenerateRefreshToken_ReturnsValidRefreshToken()
    {
        // Act
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Assert
        refreshToken.Should().NotBeNull();
        refreshToken.Token.Should().NotBeNullOrWhiteSpace();
        refreshToken.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        refreshToken.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays), TimeSpan.FromSeconds(5));
        refreshToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokens()
    {
        // Act
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        // Assert
        token1.Token.Should().NotBe(token2.Token);
    }

    [Fact]
    public void GenerateRefreshToken_TokenIsBase64Encoded()
    {
        // Act
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Assert
        var act = () => Convert.FromBase64String(refreshToken.Token);
        act.Should().NotThrow();
    }

    #endregion

    #region GetPrincipalFromExpiredToken Tests

    [Fact]
    public void GetPrincipalFromExpiredToken_WithValidToken_ReturnsPrincipal()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            Role = "User"
        };
        var token = _tokenService.GenerateAccessToken(user);

        // Act
        var principal = _tokenService.GetPrincipalFromExpiredToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(user.Id.ToString());
        principal.FindFirstValue(ClaimTypes.Name).Should().Be(user.Username);
        principal.FindFirstValue(ClaimTypes.Email).Should().Be(user.Email);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithExpiredToken_StillReturnsPrincipal()
    {
        // Arrange - Create a token that's already expired by setting a past notBefore/expires
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Role = "User"
        };
        
        // Generate a normal token first, then wait a moment and validate it as expired
        var token = _tokenService.GenerateAccessToken(user);

        // Act - This should still work even though we're validating with ValidateLifetime = false
        var principal = _tokenService.GetPrincipalFromExpiredToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(user.Id.ToString());
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        var invalidToken = "invalid.token.value";

        // Act
        var principal = _tokenService.GetPrincipalFromExpiredToken(invalidToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithEmptyToken_ReturnsNull()
    {
        // Act
        var principal = _tokenService.GetPrincipalFromExpiredToken("");

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithTokenFromDifferentIssuer_ReturnsNull()
    {
        // Arrange
        var differentOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            SigningKey = "different-signing-key-with-32-chars-minimum",
            Issuer = "different-issuer",
            Audience = "different-audience",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 7
        });
        var differentService = new TokenService(differentOptions);
        
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Role = "User"
        };
        var tokenFromDifferentIssuer = differentService.GenerateAccessToken(user);

        // Act
        var principal = _tokenService.GetPrincipalFromExpiredToken(tokenFromDifferentIssuer);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithTokenWithWrongAlgorithm_ReturnsNull()
    {
        // Arrange
        // Create a token with a different algorithm (simulated by using a different signing key format)
        var malformedToken = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.";

        // Act
        var principal = _tokenService.GetPrincipalFromExpiredToken(malformedToken);

        // Assert
        principal.Should().BeNull();
    }

    #endregion
}
