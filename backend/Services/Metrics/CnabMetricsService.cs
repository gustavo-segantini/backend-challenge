using Prometheus;
using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Services.Metrics;

/// <summary>
/// Service for exposing custom Prometheus metrics for CNAB API.
/// Provides counters, gauges, and histograms for monitoring application behavior.
/// </summary>
[ExcludeFromCodeCoverage]
public static class CnabMetricsService
{
    // File Upload Metrics
    private static readonly Counter FileUploadsTotal = Prometheus.Metrics
        .CreateCounter("cnab_file_uploads_total", "Total number of file uploads", new[] { "status" });

    private static readonly Histogram FileUploadSizeBytes = Prometheus.Metrics
        .CreateHistogram("cnab_file_upload_size_bytes", "Size of uploaded files in bytes", new[] { "status" });

    private static readonly Histogram FileUploadProcessingDurationSeconds = Prometheus.Metrics
        .CreateHistogram("cnab_file_upload_processing_duration_seconds", "Time taken to process file uploads", new[] { "status" });

    // Transaction Metrics
    private static readonly Counter TransactionsProcessedTotal = Prometheus.Metrics
        .CreateCounter("cnab_transactions_processed_total", "Total number of transactions processed", new[] { "type", "status" });

    private static readonly Gauge TransactionsInDatabase = Prometheus.Metrics
        .CreateGauge("cnab_transactions_in_database", "Current number of transactions in database");

    // Queue Metrics
    private static readonly Gauge ProcessingQueueSize = Prometheus.Metrics
        .CreateGauge("cnab_processing_queue_size", "Current size of the processing queue");

    private static readonly Counter QueueOperationsTotal = Prometheus.Metrics
        .CreateCounter("cnab_queue_operations_total", "Total queue operations", new[] { "operation", "status" });

    // Error Metrics
    private static readonly Counter ProcessingErrorsTotal = Prometheus.Metrics
        .CreateCounter("cnab_processing_errors_total", "Total processing errors", new[] { "error_type", "component" });

    // API Endpoint Metrics (complement to HTTP metrics)
    private static readonly Counter ApiOperationsTotal = Prometheus.Metrics
        .CreateCounter("cnab_api_operations_total", "Total API operations", new[] { "endpoint", "method", "status" });

    /// <summary>
    /// Records a file upload attempt.
    /// </summary>
    public static void RecordFileUpload(string status, long fileSizeBytes)
    {
        FileUploadsTotal.WithLabels(status).Inc();
        FileUploadSizeBytes.WithLabels(status).Observe(fileSizeBytes);
    }

    /// <summary>
    /// Records file upload processing duration.
    /// </summary>
    public static void RecordFileUploadProcessingDuration(string status, double durationSeconds)
    {
        FileUploadProcessingDurationSeconds.WithLabels(status).Observe(durationSeconds);
    }

    /// <summary>
    /// Records a processed transaction.
    /// </summary>
    public static void RecordTransactionProcessed(string type, string status)
    {
        TransactionsProcessedTotal.WithLabels(type, status).Inc();
    }

    /// <summary>
    /// Updates the current number of transactions in the database.
    /// </summary>
    public static void UpdateTransactionsInDatabase(long count)
    {
        TransactionsInDatabase.Set(count);
    }

    /// <summary>
    /// Updates the current processing queue size.
    /// </summary>
    public static void UpdateProcessingQueueSize(long size)
    {
        ProcessingQueueSize.Set(size);
    }

    /// <summary>
    /// Records a queue operation.
    /// </summary>
    public static void RecordQueueOperation(string operation, string status)
    {
        QueueOperationsTotal.WithLabels(operation, status).Inc();
    }

    /// <summary>
    /// Records a processing error.
    /// </summary>
    public static void RecordProcessingError(string errorType, string component)
    {
        ProcessingErrorsTotal.WithLabels(errorType, component).Inc();
    }

    /// <summary>
    /// Records an API operation.
    /// </summary>
    public static void RecordApiOperation(string endpoint, string method, string status)
    {
        ApiOperationsTotal.WithLabels(endpoint, method, status).Inc();
    }
}

