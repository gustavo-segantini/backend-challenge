using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services.Interfaces;
using CnabApi.Services.UnitOfWork;

namespace CnabApi.Services.LineProcessing;

/// <summary>
/// Processes a single CNAB line with retry logic.
/// Single Responsibility: Handle line-level processing only.
/// </summary>
public interface ILineProcessor
{
    /// <summary>
    /// Processes a single line: validates, parses, inserts transaction, and records hash.
    /// Uses Unit of Work to ensure atomicity (ACID compliance).
    /// </summary>
    Task<LineProcessingResult> ProcessLineAsync(
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
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of processing a single line.
/// </summary>
public enum LineProcessingResult
{
    Success,
    Skipped,
    Failed
}

