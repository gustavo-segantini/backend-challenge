namespace CnabApi.Models.Responses;

/// <summary>
/// Response model for authentication endpoints.
/// </summary>
/// <remarks>
/// Contains tokens and user information.
/// Access token expires in 60 minutes and should be included in Authorization header.
/// Refresh token is long-lived and used to obtain new access tokens.
/// 
/// Example:
/// <code>
/// {
///   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
///   "refreshToken": "550e8400-e29b-41d4-a716-446655440000",
///   "username": "user@example.com",
///   "role": "User"
/// }
/// </code>
/// </remarks>
public class AuthResponse
{
    /// <summary>
    /// JWT access token for authenticating API requests. Valid for 60 minutes.
    /// </summary>
    /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...</example>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for obtaining new access tokens without re-authenticating.
    /// </summary>
    /// <example>550e8400-e29b-41d4-a716-446655440000</example>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Authenticated user's username.
    /// </summary>
    /// <example>user@example.com</example>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User's role for authorization (User or Admin).
    /// </summary>
    /// <example>User</example>
    public string Role { get; set; } = string.Empty;
}
