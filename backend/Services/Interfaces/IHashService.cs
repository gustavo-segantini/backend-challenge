namespace CnabApi.Services.Interfaces;

/// <summary>
/// Service for computing hash values for deduplication.
/// Centralizes hash computation logic (DRY principle).
/// </summary>
public interface IHashService
{
    /// <summary>
    /// Computes SHA256 hash of file content for duplicate detection.
    /// Returns Base64 encoded string.
    /// </summary>
    string ComputeFileHash(string content);

    /// <summary>
    /// Computes SHA256 hash of a single line for duplicate detection.
    /// Returns lowercase hexadecimal string.
    /// </summary>
    string ComputeLineHash(string line);

    /// <summary>
    /// Computes SHA256 hash of a stream for duplicate detection.
    /// Returns lowercase hexadecimal string.
    /// </summary>
    Task<string> ComputeStreamHashAsync(Stream stream, CancellationToken cancellationToken = default);
}

