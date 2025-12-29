using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services.Interfaces;
using CnabApi.Services.LineProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CnabApi.Services;

/// <summary>
/// Orchestrates CNAB file processing with parallel workers.
/// Follows Single Responsibility Principle - only orchestrates, delegates to specialized services.
/// Uses ILineProcessor for line processing and ICheckpointManager for checkpoint logic.
/// </summary>
public class CnabUploadService(
    IHashService hashService,
    ILineProcessor lineProcessor,
    ICheckpointManager checkpointManager,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<UploadProcessingOptions> options,
    ILogger<CnabUploadService> logger) : ICnabUploadService
{
    private readonly UploadProcessingOptions _options = options.Value;

    /// <summary>
    /// Processes CNAB file content line by line with parallel workers.
    /// Each line is validated, parsed, and inserted atomically with retry.
    /// Checkpoints are saved periodically for resume support.
    /// </summary>
    public async Task<Result<int>> ProcessCnabUploadAsync(
        string fileContent,
        Guid fileUploadId,
        int startFromLine = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                logger.LogWarning("File content is empty");
                return Result<int>.Failure("File was not provided or is empty.");
            }

            var lines = fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            var totalLines = lines.Length;
            
            // Set total line count - create a scope for this
            using (var initScope = serviceScopeFactory.CreateScope())
            {
                var initFileUploadTrackingService = initScope.ServiceProvider.GetRequiredService<IFileUploadTrackingService>();
                await initFileUploadTrackingService.SetTotalLineCountAsync(fileUploadId, totalLines, cancellationToken);
            }

            logger.LogInformation(
                "Starting line-by-line processing. UploadId: {UploadId}, TotalLines: {Total}, StartFrom: {Start}, Workers: {Workers}",
                fileUploadId, totalLines, startFromLine, _options.ParallelWorkers);

            // Compute file hash for idempotency keys
            var fileHash = hashService.ComputeFileHash(fileContent);

            // Thread-safe counters using Interlocked for atomic operations
            var processedCount = 0;
            var failedCount = 0;
            var skippedCount = 0;
            var lastCheckpointLine = new AtomicInt(startFromLine);

            // Lines to process (skip already processed)
            var linesToProcess = lines
                .Select((line, index) => (Line: line, Index: index))
                .Skip(startFromLine)
                .ToArray();

            if (linesToProcess.Length == 0)
            {
                logger.LogInformation("No lines to process (all already processed). UploadId: {UploadId}", fileUploadId);
                return Result<int>.Success(0);
            }

            // Use SemaphoreSlim to limit parallel workers
            using var semaphore = new SemaphoreSlim(_options.ParallelWorkers);

            // Process lines in parallel with controlled concurrency
            var tasks = new List<Task>();
            var processedLines = new ConcurrentBag<int>();

            foreach (var (line, index) in linesToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await semaphore.WaitAsync(cancellationToken);

                var task = Task.Run(async () =>
                {
                    // Create a new scope for this task to get its own DbContext and Unit of Work
                    using var scope = serviceScopeFactory.CreateScope();
                    var transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService>();
                    var fileUploadTrackingService = scope.ServiceProvider.GetRequiredService<IFileUploadTrackingService>();
                    var parserService = scope.ServiceProvider.GetRequiredService<ICnabParserService>();
                    var scopeHashService = scope.ServiceProvider.GetRequiredService<IHashService>();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<Services.UnitOfWork.IUnitOfWork>();

                    try
                    {
                        // Delegate line processing to ILineProcessor (with Unit of Work for ACID compliance)
                        var result = await lineProcessor.ProcessLineAsync(
                            line,
                            index,
                            fileUploadId,
                            fileHash,
                            transactionService,
                            fileUploadTrackingService,
                            parserService,
                            scopeHashService,
                            unitOfWork,
                            _options.MaxRetryPerLine,
                            _options.RetryDelayMs,
                            logger,
                            cancellationToken);

                        switch (result)
                        {
                            case LineProcessingResult.Success:
                                Interlocked.Increment(ref processedCount);
                                break;
                            case LineProcessingResult.Skipped:
                                Interlocked.Increment(ref skippedCount);
                                break;
                            case LineProcessingResult.Failed:
                                Interlocked.Increment(ref failedCount);
                                break;
                        }

                        processedLines.Add(index);

                        // Delegate checkpoint logic to ICheckpointManager
                        var currentProcessed = Interlocked.CompareExchange(ref processedCount, 0, 0) + 
                                               Interlocked.CompareExchange(ref failedCount, 0, 0) + 
                                               Interlocked.CompareExchange(ref skippedCount, 0, 0);
                        
                        if (checkpointManager.ShouldSaveCheckpoint(currentProcessed, _options.CheckpointInterval))
                        {
                            var maxProcessedLine = processedLines.DefaultIfEmpty(startFromLine).Max();
                            var currentLastCheckpoint = lastCheckpointLine.Value;
                            
                            // Only update if we have a new maximum (atomic compare-and-swap)
                            if (maxProcessedLine > currentLastCheckpoint && 
                                lastCheckpointLine.CompareAndSwap(currentLastCheckpoint, maxProcessedLine))
                            {
                                // Fire and forget checkpoint save (non-blocking)
                                _ = checkpointManager.SaveCheckpointAsync(
                                    fileUploadId,
                                    maxProcessedLine,
                                    Interlocked.CompareExchange(ref processedCount, 0, 0),
                                    Interlocked.CompareExchange(ref failedCount, 0, 0),
                                    Interlocked.CompareExchange(ref skippedCount, 0, 0),
                                    fileUploadTrackingService,
                                    logger,
                                    cancellationToken);
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Final checkpoint - save checkpoint before final status update to ensure progress is saved
            using (var finalScope = serviceScopeFactory.CreateScope())
            {
                var finalFileUploadTrackingService = finalScope.ServiceProvider.GetRequiredService<IFileUploadTrackingService>();
                var finalMaxLine = processedLines.DefaultIfEmpty(startFromLine).Max();
                
                // Save final checkpoint to ensure progress is persisted even if final status update fails
                if (finalMaxLine > startFromLine)
                {
                    await checkpointManager.SaveCheckpointAsync(
                        fileUploadId,
                        finalMaxLine,
                        processedCount,
                        failedCount,
                        skippedCount,
                        finalFileUploadTrackingService,
                        logger,
                        cancellationToken);
                }

                // Update final processing result
                await finalFileUploadTrackingService.UpdateProcessingResultAsync(
                    fileUploadId,
                    processedCount,
                    failedCount,
                    skippedCount,
                    cancellationToken);
            }

            logger.LogInformation(
                "Processing completed. UploadId: {UploadId}, Processed: {Processed}, Failed: {Failed}, Skipped: {Skipped}",
                fileUploadId, processedCount, failedCount, skippedCount);

            // If no lines were processed successfully and there were failures, consider it a validation failure
            if (processedCount == 0 && failedCount > 0)
            {
                logger.LogWarning(
                    "Upload validation failed. UploadId: {UploadId}, All {Failed} lines failed validation",
                    fileUploadId, failedCount);
                
                return Result<int>.Failure($"Invalid CNAB file format - all {failedCount} lines failed validation (expected 80 characters per line)");
            }

            return Result<int>.Success(processedCount);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Processing cancelled. UploadId: {UploadId}", fileUploadId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during processing. UploadId: {UploadId}", fileUploadId);
            return Result<int>.Failure($"Processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Thread-safe atomic integer wrapper for checkpoint line tracking.
    /// Simplifies checkpoint logic (KISS principle).
    /// </summary>
    private class AtomicInt
    {
        private int _value;

        public AtomicInt(int initialValue)
        {
            _value = initialValue;
        }

        public int Value => Interlocked.CompareExchange(ref _value, 0, 0);

        public bool CompareAndSwap(int expected, int newValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, expected) == expected;
        }
    }
}
