using CnabApi.Models.Requests;
using CnabApi.Validators;
using FluentAssertions;

namespace CnabApi.Tests.Services.Auth;

/// <summary>
/// Unit tests for request validators.
/// </summary>
public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "user123",
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyUsername_ReturnsFail()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = string.Empty,
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithShortUsername_ReturnsFail()
    {
        // Arrange - Username requires minimum 3 characters
        var request = new RegisterRequest
        {
            Username = "ab",
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithLongUsername_ReturnsFail()
    {
        // Arrange - Username max 50 characters
        var request = new RegisterRequest
        {
            Username = new string('a', 51),
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithInvalidUsernameChars_ReturnsFail()
    {
        // Arrange - Username with invalid characters (allows only letters, numbers, _, -)
        var request = new RegisterRequest
        {
            Username = "user@invalid",
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyPassword_ReturnsFail()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "user123",
            Password = string.Empty
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithShortPassword_ReturnsFail()
    {
        // Arrange - Password requires minimum 6 characters
        var request = new RegisterRequest
        {
            Username = "user123",
            Password = "Pass1"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithLongPassword_ReturnsFail()
    {
        // Arrange - Password max 100 characters
        var request = new RegisterRequest
        {
            Username = "user123",
            Password = new string('a', 101)
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "user@example.com",
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyUsername_ReturnsFail()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = string.Empty,
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyPassword_ReturnsFail()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "user@example.com",
            Password = string.Empty
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithSpaceUsername_ReturnsFail()
    {
        // Arrange - LoginValidator checks NotEmpty which also rejects whitespace-only strings
        var request = new LoginRequest
        {
            Username = "  ",
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithMinimalPassword_ReturnsSuccess()
    {
        // Arrange - LoginValidator only checks NotEmpty, minimum length not validated
        var request = new LoginRequest
        {
            Username = "user@example.com",
            Password = "a"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullUsername_ReturnsFail()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = null!,
            Password = "SecurePass123"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNullPassword_ReturnsFail()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "user@example.com",
            Password = null!
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
