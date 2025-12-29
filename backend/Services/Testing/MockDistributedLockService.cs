using CnabApi.Services.Interfaces;

namespace CnabApi.Services.Testing;

/// <summary>
/// Mock implementation of IDistributedLockService for testing environments.
/// Provides in-memory lock functionality without requiring Redis.
/// </summary>
public class MockDistributedLockService : IDistributedLockService
{
    private readonly Dictionary<string, string> _locks = [];
    private readonly ILogger<MockDistributedLockService> _logger;

    public MockDistributedLockService(ILogger<MockDistributedLockService> logger)
    {
        _logger = logger;
    }

    public Task<bool> AcquireLockAsync(string key, string lockValue, int expirationSeconds = 300, CancellationToken cancellationToken = default)
    {
        lock (_locks)
        {
            if (_locks.ContainsKey(key))
            {
                _logger.LogInformation("MockDistributedLockService: Failed to acquire lock for '{Key}' (already locked)", key);
                return Task.FromResult(false);
            }

            _locks[key] = lockValue;
            _logger.LogInformation("MockDistributedLockService: Acquired lock for '{Key}'", key);
            return Task.FromResult(true);
        }
    }

    public Task<bool> ReleaseLockAsync(string key, string lockValue, CancellationToken cancellationToken = default)
    {
        lock (_locks)
        {
            if (_locks.TryGetValue(key, out var storedValue) && storedValue == lockValue)
            {
                _locks.Remove(key);
                _logger.LogInformation("MockDistributedLockService: Released lock for '{Key}'", key);
                return Task.FromResult(true);
            }

            _logger.LogWarning("MockDistributedLockService: Failed to release lock for '{Key}' (lock value mismatch or not found)", key);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> ExecuteWithLockAsync(string key, Func<Task> action, int expirationSeconds = 300, CancellationToken cancellationToken = default)
    {
        if (!await AcquireLockAsync(key, Guid.NewGuid().ToString(), expirationSeconds, cancellationToken))
        {
            return false;
        }

        try
        {
            await action();
            return true;
        }
        finally
        {
            await ReleaseLockAsync(key, "", cancellationToken);
        }
    }

    public async Task<(bool LockAcquired, T? Result)> ExecuteWithLockAsync<T>(string key, Func<Task<T>> action, int expirationSeconds = 300, CancellationToken cancellationToken = default)
    {
        if (!await AcquireLockAsync(key, Guid.NewGuid().ToString(), expirationSeconds, cancellationToken))
        {
            return (false, default);
        }

        try
        {
            var result = await action();
            return (true, result);
        }
        finally
        {
            await ReleaseLockAsync(key, "", cancellationToken);
        }
    }

    public Task<bool> LockExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_locks)
        {
            var exists = _locks.ContainsKey(key);
            _logger.LogInformation("MockDistributedLockService: Lock existence check for '{Key}': {Exists}", key, exists);
            return Task.FromResult(exists);
        }
    }
}
