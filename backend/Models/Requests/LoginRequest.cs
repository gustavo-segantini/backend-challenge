namespace CnabApi.Models.Requests;

/// <summary>
/// Request model for user login.
/// </summary>
/// <remarks>
/// Example:
/// <code>
/// {
///   "username": "user@example.com",
///   "password": "SecurePass123!"
/// }
/// </code>
/// </remarks>
public class LoginRequest
{
    /// <summary>
    /// User's email or username.
    /// </summary>
    /// <example>user@example.com</example>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's password.
    /// </summary>
    /// <example>SecurePass123!</example>
    public string Password { get; set; } = string.Empty;
}
