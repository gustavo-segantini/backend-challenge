namespace CnabApi.Models.Requests;

/// <summary>
/// Request model for user registration.
/// </summary>
/// <remarks>
/// Example:
/// <code>
/// {
///   "username": "user@example.com",
///   "password": "SecurePass123!"
/// }
/// </code>
/// 
/// Password Requirements:
/// - Minimum 8 characters
/// - At least 1 uppercase letter
/// - At least 1 digit
/// - At least 1 special character
/// </remarks>
public class RegisterRequest
{
    /// <summary>
    /// User's email or username for login.
    /// </summary>
    /// <example>user@example.com</example>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's email (optional).
    /// </summary>
    /// <example>user@example.com</example>
    public string? Email { get; set; }

    /// <summary>
    /// Secure password (8+ chars with uppercase, digit, and special char).
    /// </summary>
    /// <example>SecurePass123!</example>
    public string Password { get; set; } = string.Empty;
}
