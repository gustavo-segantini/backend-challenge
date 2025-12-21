namespace CnabApi.Models.Requests;

/// <summary>
/// Request model for token refresh.
/// </summary>
/// <remarks>
/// Used to obtain a new access token when the current one expires.
/// </remarks>
public class RefreshRequest
{
    /// <summary>
    /// Refresh token obtained during login or registration.
    /// </summary>
    /// <example>550e8400-e29b-41d4-a716-446655440000</example>
    public string RefreshToken { get; set; } = string.Empty;
}
