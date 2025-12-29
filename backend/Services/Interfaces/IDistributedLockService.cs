namespace CnabApi.Services.Interfaces;

/// <summary>
/// Service for distributed locking using Redis.
/// Prevents concurrent processing of the same file upload.
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Attempts to acquire a distributed lock.
    /// </summary>
    /// <param name="key">Lock key identifier</param>
    /// <param name="lockValue">Unique value for this lock instance</param>
    /// <param name="expirationSeconds">Lock expiration time in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock was acquired, false if already held by another process</returns>
    Task<bool> AcquireLockAsync(string key, string lockValue, int expirationSeconds = 300, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a distributed lock.
    /// </summary>
    /// <param name="key">Lock key identifier</param>
    /// <param name="lockValue">The lock value that was set during acquisition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock was released, false if lock value didn't match</returns>
    Task<bool> ReleaseLockAsync(string key, string lockValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action with automatic lock acquisition and release.
    /// </summary>
    /// <param name="key">Lock key identifier</param>
    /// <param name="action">Action to execute while holding the lock</param>
    /// <param name="expirationSeconds">Lock expiration time in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if action was executed, false if lock could not be acquired</returns>
    Task<bool> ExecuteWithLockAsync(string key, Func<Task> action, int expirationSeconds = 300, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action with automatic lock acquisition and release (with return value).
    /// </summary>
    /// <param name="key">Lock key identifier</param>
    /// <param name="action">Action to execute while holding the lock</param>
    /// <param name="expirationSeconds">Lock expiration time in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (lockAcquired, result)</returns>
    Task<(bool LockAcquired, T? Result)> ExecuteWithLockAsync<T>(string key, Func<Task<T>> action, int expirationSeconds = 300, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a lock exists (is currently held) without attempting to acquire it.
    /// </summary>
    /// <param name="key">Lock key identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock exists, false otherwise</returns>
    Task<bool> LockExistsAsync(string key, CancellationToken cancellationToken = default);
}
