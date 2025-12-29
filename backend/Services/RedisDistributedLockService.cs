using StackExchange.Redis;
using CnabApi.Services.Interfaces;

namespace CnabApi.Services;

/// <summary>
/// Redis-based distributed lock service.
/// Uses SET with NX (Not eXists) option and unique lock values for safe distributed locking.
/// </summary>
public class RedisDistributedLockService(IConnectionMultiplexer redis, ILogger<RedisDistributedLockService> logger) : IDistributedLockService
{
    private const string LockKeyPrefix = "lock:";

    public async Task<bool> AcquireLockAsync(string key, string lockValue, int expirationSeconds = 300, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var lockKey = $"{LockKeyPrefix}{key}";

            // SET key value EX expiration NX (only if not exists)
            var success = await db.StringSetAsync(
                lockKey,
                lockValue,
                TimeSpan.FromSeconds(expirationSeconds),
                When.NotExists);

            if (success)
            {
                logger.LogDebug("Lock acquired. Key: {LockKey}, ExpirationSeconds: {ExpirationSeconds}", lockKey, expirationSeconds);
            }
            else
            {
                logger.LogDebug("Lock already held by another process. Key: {LockKey}", lockKey);
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error acquiring lock. Key: {Key}", key);
            throw;
        }
    }

    public async Task<bool> ReleaseLockAsync(string key, string lockValue, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var lockKey = $"{LockKeyPrefix}{key}";

            // Use Lua script to atomically check value and delete
            // This ensures we only delete our own lock
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            var result = await db.ScriptEvaluateAsync(script, new RedisKey[] { lockKey }, new RedisValue[] { lockValue });
            var success = (long)result! > 0;

            if (success)
            {
                logger.LogDebug("Lock released. Key: {LockKey}", lockKey);
            }
            else
            {
                logger.LogWarning("Failed to release lock (value mismatch or lock not found). Key: {LockKey}", lockKey);
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error releasing lock. Key: {Key}", key);
            throw;
        }
    }

    public async Task<bool> ExecuteWithLockAsync(string key, Func<Task> action, int expirationSeconds = 300, CancellationToken cancellationToken = default)
    {
        var lockValue = Guid.NewGuid().ToString();

        if (!await AcquireLockAsync(key, lockValue, expirationSeconds, cancellationToken))
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
            await ReleaseLockAsync(key, lockValue, cancellationToken);
        }
    }

    public async Task<(bool LockAcquired, T? Result)> ExecuteWithLockAsync<T>(
        string key,
        Func<Task<T>> action,
        int expirationSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        var lockValue = Guid.NewGuid().ToString();

        if (!await AcquireLockAsync(key, lockValue, expirationSeconds, cancellationToken))
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
            await ReleaseLockAsync(key, lockValue, cancellationToken);
        }
    }

    public async Task<bool> LockExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var lockKey = $"{LockKeyPrefix}{key}";
            
            var exists = await db.KeyExistsAsync(lockKey);
            
            logger.LogDebug("Lock existence check. Key: {LockKey}, Exists: {Exists}", lockKey, exists);
            
            return exists;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking lock existence. Key: {Key}", key);
            // If we can't check, assume lock exists to be safe (don't recover)
            return true;
        }
    }
}
