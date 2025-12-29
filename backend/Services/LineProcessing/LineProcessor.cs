using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services.Interfaces;
using CnabApi.Services.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CnabApi.Services.LineProcessing;

/// <summary>
/// Processes a single CNAB line with retry logic.
/// Follows Single Responsibility Principle - only handles line processing.
/// Uses Unit of Work to ensure ACID compliance.
/// </summary>
public class LineProcessor : ILineProcessor
{
    public async Task<LineProcessingResult> ProcessLineAsync(
        string line,
        int lineIndex,
        Guid fileUploadId,
        string fileHash,
        ITransactionService transactionService,
        IFileUploadTrackingService fileUploadTrackingService,
        ICnabParserService parserService,
        IHashService hashService,
        IUnitOfWork unitOfWork,
        int maxRetries,
        int retryDelayMs,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Step 1: Check for duplicate line (before transaction)
                var lineHash = hashService.ComputeLineHash(line);
                var isUnique = await fileUploadTrackingService.IsLineUniqueAsync(lineHash, cancellationToken);

                if (!isUnique)
                {
                    logger.LogDebug(
                        "Skipping duplicate line. UploadId: {UploadId}, LineIndex: {Index}",
                        fileUploadId, lineIndex);
                    return LineProcessingResult.Skipped;
                }

                // Step 2: Parse line
                var parseResult = parserService.ParseCnabLine(line, lineIndex);
                if (!parseResult.IsSuccess || parseResult.Data == null)
                {
                    logger.LogWarning(
                        "Failed to parse line. UploadId: {UploadId}, LineIndex: {Index}, Error: {Error}",
                        fileUploadId, lineIndex, parseResult.ErrorMessage);
                    return LineProcessingResult.Failed;
                }

                var transaction = parseResult.Data;
                transaction.IdempotencyKey = GenerateIdempotencyKey(fileHash, lineIndex);
                transaction.FileUploadId = fileUploadId;

                // Step 3 & 4: Insert transaction AND record line hash atomically using Unit of Work (ACID)
                try
                {
                    // Execute both operations atomically within a single transaction (ACID compliance)
                    var committedTransaction = await unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        // Step 3: Insert transaction (without SaveChanges - Unit of Work will handle it)
                        var insertResult = await transactionService.AddTransactionToContextAsync(transaction, cancellationToken);

                        if (!insertResult.IsSuccess)
                        {
                            // Check if it's a duplicate (idempotency)
                            if (insertResult.ErrorMessage?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
                                insertResult.ErrorMessage?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                logger.LogDebug(
                                    "Transaction already exists (idempotent). UploadId: {UploadId}, LineIndex: {Index}",
                                    fileUploadId, lineIndex);
                                throw new InvalidOperationException("Transaction already exists (idempotent)");
                            }

                            throw new InvalidOperationException(insertResult.ErrorMessage ?? "Insert failed");
                        }

                        // Step 4: Record line hash for future duplicate detection (same transaction)
                        // Note: RecordLineHashAsync adds to ChangeTracker but doesn't SaveChanges
                        await fileUploadTrackingService.RecordLineHashAsync(
                            fileUploadId,
                            lineHash,
                            line,
                            cancellationToken);

                        // Unit of Work will call SaveChangesAsync for both operations atomically
                        return insertResult.Data!;
                    }, cancellationToken);

                    logger.LogDebug(
                        "Line processed successfully (committed atomically). UploadId: {UploadId}, LineIndex: {Index}",
                        fileUploadId, lineIndex);

                    return LineProcessingResult.Success;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("idempotent", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle idempotent duplicate (detected before insert)
                    logger.LogDebug(
                        "Transaction already exists (idempotent). UploadId: {UploadId}, LineIndex: {Index}",
                        fileUploadId, lineIndex);
                    return LineProcessingResult.Skipped;
                }
                catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
                {
                    // Handle unique violation from database (detected during insert)
                    logger.LogDebug(
                        "Transaction already exists (unique constraint). UploadId: {UploadId}, LineIndex: {Index}",
                        fileUploadId, lineIndex);
                    return LineProcessingResult.Skipped;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex,
                    "Line processing failed (attempt {Attempt}/{Max}). UploadId: {UploadId}, LineIndex: {Index}",
                    attempt, maxRetries, fileUploadId, lineIndex);

                await Task.Delay(retryDelayMs * attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Line processing failed after all retries. UploadId: {UploadId}, LineIndex: {Index}",
                    fileUploadId, lineIndex);
                return LineProcessingResult.Failed;
            }
        }

        return LineProcessingResult.Failed;
    }

    private static string GenerateIdempotencyKey(string fileHash, int lineIndex)
    {
        return $"{fileHash}:{lineIndex}";
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            // PostgreSQL unique violation error code
            return pgEx.SqlState == "23505";
        }
        return false;
    }
}

