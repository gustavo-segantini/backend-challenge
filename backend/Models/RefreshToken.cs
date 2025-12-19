using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CnabApi.Models;

/// <summary>
/// Refresh token stored in the database for long-lived sessions.
/// </summary>
public class RefreshToken
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiresAt { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public bool IsActive => RevokedAt == null && DateTime.UtcNow <= ExpiresAt;
}
