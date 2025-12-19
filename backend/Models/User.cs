using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CnabApi.Models;

/// <summary>
/// Represents an application user for authentication/authorization.
/// </summary>
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(320)]
    public string? Email { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Role { get; set; } = "User";

    // OAuth data (GitHub)
    [MaxLength(100)]
    public string? GitHubId { get; set; }

    [MaxLength(150)]
    public string? GitHubUsername { get; set; }

    [MaxLength(300)]
    public string? GitHubAvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
