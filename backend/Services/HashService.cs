using System.Security.Cryptography;
using System.Text;
using CnabApi.Services.Interfaces;

namespace CnabApi.Services;

/// <summary>
/// Centralized hash computation service.
/// Implements DRY principle by consolidating all hash calculations.
/// </summary>
public class HashService : IHashService
{
    /// <summary>
    /// Computes SHA256 hash of file content for duplicate detection.
    /// Returns Base64 encoded string (for consistency with existing code).
    /// </summary>
    public string ComputeFileHash(string content)
    {
        if (string.IsNullOrEmpty(content))
            throw new ArgumentException("Content cannot be null or empty", nameof(content));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Computes SHA256 hash of a single line for duplicate detection.
    /// Returns lowercase hexadecimal string.
    /// </summary>
    public string ComputeLineHash(string line)
    {
        if (string.IsNullOrEmpty(line))
            throw new ArgumentException("Line cannot be null or empty", nameof(line));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(line));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Computes SHA256 hash of a stream for duplicate detection.
    /// Returns lowercase hexadecimal string.
    /// </summary>
    public async Task<string> ComputeStreamHashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        // Ensure we're at the beginning of the stream
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        using var sha256 = SHA256.Create();
        var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), cancellationToken);

        // Reset stream position for further reading
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        return Convert.ToHexStringLower(hashBytes);
    }
}

