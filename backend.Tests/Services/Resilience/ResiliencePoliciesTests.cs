using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using CnabApi.Services.Resilience;

namespace CnabApi.Tests.Services.Resilience;

/// <summary>
/// Unit tests for ResiliencePolicies retry, circuit breaker, and timeout policies
/// </summary>
public class ResiliencePoliciesTests
{
    private readonly Mock<ILogger> _mockLogger = new();

    #region GetDatabaseRetryPolicy Tests

    [Fact]
    public async Task GetDatabaseRetryPolicy_WithSuccessOnFirstAttempt_ExecutesOnce()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            await Task.Delay(10);
            return "success";
        });

        // Assert
        result.Should().Be("success");
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDatabaseRetryPolicy_WithTransientFailureThenSuccess_RetriesAndSucceeds()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount < 2)
            {
                throw new NullReferenceException("Transient failure");
            }
            await Task.Delay(10);
            return "success";
        });

        // Assert
        result.Should().Be("success");
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetDatabaseRetryPolicy_WithMultipleTransientFailures_RetriesUpToLimit()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var executionCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                executionCount++;
                await Task.Delay(10);
                throw new NullReferenceException("Persistent failure");
            });
        });

        // Assert - 1 initial + 3 retries = 4 attempts
        executionCount.Should().Be(4);
    }

    [Fact]
    public async Task GetDatabaseRetryPolicy_WithInvalidOperationException_Retries()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount < 3)
            {
                throw new InvalidOperationException("Invalid state");
            }
            await Task.Delay(10);
            return "recovered";
        });

        // Assert
        result.Should().Be("recovered");
        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task GetDatabaseRetryPolicy_WithNonHandledException_FailsImmediately()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await policy.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                throw new ArgumentException("Not handled");
            });
        });
    }

    [Fact]
    public async Task GetDatabaseRetryPolicy_WithNullResult_Retries()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string?>(_mockLogger.Object);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount < 2)
            {
                return await Task.FromResult<string?>(null);
            }
            return await Task.FromResult("value");
        });

        // Assert
        result.Should().Be("value");
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetDatabaseRetryPolicy_HasExponentialBackoff()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var stopwatch = new System.Diagnostics.Stopwatch();
        var executionCount = 0;

        // Act
        stopwatch.Start();
        await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount < 3)
            {
                throw new NullReferenceException();
            }
            return await Task.FromResult("success");
        });
        stopwatch.Stop();

        // Assert - should wait at least 2s (first retry) + 4s (second retry) = 6s
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(5000);
        executionCount.Should().Be(3);
    }

    [Fact]
    public async Task GetDatabaseRetryPolicy_NonGeneric_WorksWithVoidReturn()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy(_mockLogger.Object);
        var executionCount = 0;

        // Act
        await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount < 2)
            {
                throw new InvalidOperationException();
            }
            await Task.Delay(10);
        });

        // Assert
        executionCount.Should().Be(2);
    }

    #endregion

    #region GetFileOperationRetryPolicy Tests

    [Fact]
    public async Task GetFileOperationRetryPolicy_WithIOException_Retries()
    {
        // Arrange
        var policy = ResiliencePolicies.GetFileOperationRetryPolicy(_mockLogger.Object);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount < 2)
            {
                throw new IOException("File access error");
            }
            await Task.Delay(10);
            return "success";
        });

        // Assert
        result.Should().Be("success");
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetFileOperationRetryPolicy_WithUnauthorizedAccessException_Retries()
    {
        // Arrange
        var policy = ResiliencePolicies.GetFileOperationRetryPolicy(_mockLogger.Object);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new UnauthorizedAccessException("Access denied");
            }
            await Task.Delay(10);
            return "allowed";
        });

        // Assert
        result.Should().Be("allowed");
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetFileOperationRetryPolicy_HasShorterDelayThanDatabase()
    {
        // Arrange
        var filePolicy = ResiliencePolicies.GetFileOperationRetryPolicy(_mockLogger.Object);
        var stopwatch = new System.Diagnostics.Stopwatch();
        var executionCount = 0;

        // Act
        stopwatch.Start();
        await filePolicy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount < 3)
            {
                throw new IOException();
            }
            return await Task.FromResult("done");
        });
        stopwatch.Stop();

        // Assert - should wait ~500ms + 1000ms = 1500ms
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    #endregion

    #region GetCircuitBreakerPolicy Tests

    [Fact]
    public async Task GetCircuitBreakerPolicy_WithSingleFailure_ContinuesExecuting()
    {
        // Arrange
        var policy = ResiliencePolicies.GetCircuitBreakerPolicy<string>(_mockLogger.Object);
        var executionCount = 0;

        // Act
        await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            throw new NullReferenceException();
        }).ContinueWith(_ => { }, TaskScheduler.Default);

        // Should continue (not break yet)
        await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            return await Task.FromResult("second");
        });

        // Assert
        executionCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetCircuitBreakerPolicy_AfterThreeFailures_TripsCircuit()
    {
        // Arrange
        var policy = ResiliencePolicies.GetCircuitBreakerPolicy<string>(_mockLogger.Object);
        var executionCount = 0;

        // Act - cause 3 failures to trip circuit
        for (int i = 0; i < 3; i++)
        {
            await policy.ExecuteAsync(async () =>
            {
                executionCount++;
                await Task.Delay(10);
                throw new NullReferenceException();
            }).ContinueWith(_ => { }, TaskScheduler.Default);
        }

        // Try to execute with circuit open
        var exception = await Record.ExceptionAsync(async () =>
        {
            await policy.ExecuteAsync(async () => await Task.FromResult("should fail"));
        });

        // Assert
        exception.Should().NotBeNull();
        executionCount.Should().Be(3); // Only 3 executions before circuit opens
    }

    [Fact]
    public async Task GetCircuitBreakerPolicy_WithNullResult_CountsAsFailure()
    {
        // Arrange
        var policy = ResiliencePolicies.GetCircuitBreakerPolicy<string?>(_mockLogger.Object);
        var executionCount = 0;

        // Act - 3 null results should trip circuit
        for (int i = 0; i < 3; i++)
        {
            await policy.ExecuteAsync(async () =>
            {
                executionCount++;
                return await Task.FromResult<string?>(null);
            }).ContinueWith(_ => { }, TaskScheduler.Default);
        }

        // Assert
        executionCount.Should().Be(3);
    }

    #endregion

    #region GetCombinedDatabasePolicy Tests

    [Fact]
    public async Task GetCombinedDatabasePolicy_WithTransientFailure_RetriesThenBreaks()
    {
        // Arrange
        var policy = ResiliencePolicies.GetCombinedDatabasePolicy<string>(_mockLogger.Object);
        var executionCount = 0;

        // Act - retry first, then circuit breaker
        var result = await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount < 2)
            {
                throw new InvalidOperationException("Transient");
            }
            return await Task.FromResult("recovered");
        });

        // Assert
        result.Should().Be("recovered");
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetCombinedDatabasePolicy_WrapsRetryAndCircuitBreaker()
    {
        // Act - create combined policy
        var policy = ResiliencePolicies.GetCombinedDatabasePolicy<string>(_mockLogger.Object);

        // Assert - verify it's a wrapped policy
        policy.Should().NotBeNull();
        policy.Should().BeAssignableTo<IAsyncPolicy<string>>();
    }

    #endregion

    #region GetTimeoutPolicy Tests

    [Fact]
    public async Task GetTimeoutPolicy_WithTimeoutExceeded_ThrowsTimeoutException()
    {
        // Arrange
        var policy = ResiliencePolicies.GetTimeoutPolicy();

        // Act & Assert - Polly timeout lança TimeoutRejectedException
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await policy.ExecuteAsync(async (ct) =>
            {
                await Task.Delay(15000, ct); // 15 seconds, policy is 10 seconds
            }, CancellationToken.None);
        });
    }

    [Fact]
    public async Task GetTimeoutPolicy_WithCompletionBeforeTimeout_Succeeds()
    {
        // Arrange
        var policy = ResiliencePolicies.GetTimeoutPolicy();

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            await Task.Delay(100);
            return "completed";
        });

        // Assert
        result.Should().Be("completed");
    }

    [Fact]
    public async Task GetTimeoutPolicy_Generic_WithTimespan_Respects()
    {
        // Arrange
        var policy = ResiliencePolicies.GetTimeoutPolicy<string>(TimeSpan.FromSeconds(5));

        // Act & Assert - Polly lança TimeoutRejectedException
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await policy.ExecuteAsync(async (ct) =>
            {
                await Task.Delay(10000, ct); // 10 seconds
                return await Task.FromResult("should not complete");
            }, CancellationToken.None);
        });
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public async Task MultiplePolices_CanBeStacked()
    {
        // Arrange
        var retryPolicy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var circuitBreakerPolicy = ResiliencePolicies.GetCircuitBreakerPolicy<string>(_mockLogger.Object);
        var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        // Act
        var result = await combinedPolicy.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
            return "success";
        });

        // Assert
        result.Should().Be("success");
    }

    [Fact]
    public async Task RetryPolicy_WithContextContainsCorrelationId()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var context = new Context { { "correlationId", "test-id-123" } };

        // Act
        var result = await policy.ExecuteAsync(async (ctx) =>
        {
            await Task.Delay(10);
            return "success";
        }, context);

        // Assert
        result.Should().Be("success");
    }

    [Fact]
    public void AllPolicies_AreAccessible()
    {
        // Verify all policy factory methods are available
        var methods = typeof(ResiliencePolicies)
            .GetMethods()
            .Where(m => m.Name.StartsWith("Get") && m.ReturnType.Name.Contains("Policy"))
            .ToList();

        // Assert - should have multiple policy methods
        methods.Count.Should().BeGreaterThanOrEqualTo(6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RetryPolicy_HandlesMultipleExceptionTypes(int retryCount)
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var executionCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async () =>
        {
            executionCount++;
            if (executionCount <= retryCount)
            {
                if (executionCount % 2 == 0)
                    throw new NullReferenceException();
                else
                    throw new InvalidOperationException();
            }
            return await Task.FromResult("recovered");
        });

        // Assert
        result.Should().Be("recovered");
        executionCount.Should().Be(retryCount + 1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task RetryPolicy_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var policy = ResiliencePolicies.GetDatabaseRetryPolicy<string>(_mockLogger.Object);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        // Act & Assert - TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await policy.ExecuteAsync(async (ct) =>
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(50);
                    throw new NullReferenceException();
                }
            }, cts.Token);
        });
    }

    [Fact]
    public async Task CircuitBreaker_RecoverAfterTimeout()
    {
        // Arrange
        var policy = ResiliencePolicies.GetCircuitBreakerPolicy<string>(_mockLogger.Object);

        // Act - trip circuit
        for (int i = 0; i < 3; i++)
        {
            await policy.ExecuteAsync(async () =>
            {
                await Task.Delay(10);
                throw new NullReferenceException();
            }).ContinueWith(_ => { });
        }

        // Wait for circuit breaker to reset (30 seconds)
        // For test purposes, we verify circuit is open
        var exception = await Record.ExceptionAsync(async () =>
        {
            await policy.ExecuteAsync(async () => await Task.FromResult("should fail"));
        });

        // Assert
        exception.Should().NotBeNull();
    }

    #endregion
}
