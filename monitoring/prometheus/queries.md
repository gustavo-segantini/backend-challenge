# Prometheus Queries - CNAB API

Useful Prometheus queries with filters for monitoring the CNAB API.

## HTTP Metrics

### Request Rate by Endpoint
```promql
rate(http_requests_received_total[5m])
```

### Request Rate by Status Code
```promql
rate(http_requests_received_total{code=~"2.."}[5m])  # Success (2xx)
rate(http_requests_received_total{code=~"4.."}[5m])  # Client errors (4xx)
rate(http_requests_received_total{code=~"5.."}[5m])  # Server errors (5xx)
```

### Request Rate for Specific Endpoint
```promql
rate(http_requests_received_total{route="/api/v1/transactions/upload"}[5m])
rate(http_requests_received_total{route="/api/v1/transactions/uploads"}[5m])
rate(http_requests_received_total{route="/api/v1/transactions/stores/{uploadId}"}[5m])
```

### Response Time (P95) by Endpoint
```promql
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))
```

### Response Time (P99) for Specific Endpoint
```promql
histogram_quantile(0.99, rate(http_request_duration_seconds_bucket{route="/api/v1/transactions/upload"}[5m]))
```

### Error Rate Percentage
```promql
(rate(http_requests_received_total{code=~"5.."}[5m]) / rate(http_requests_received_total[5m])) * 100
```

## Custom CNAB Metrics

### File Upload Rate
```promql
rate(cnab_file_uploads_total[5m])
```

### File Upload Rate by Status
```promql
rate(cnab_file_uploads_total{status="success"}[5m])   # Successful uploads
rate(cnab_file_uploads_total{status="failed"}[5m])    # Failed uploads
rate(cnab_file_uploads_total{status="pending"}[5m])  # Pending uploads
```

### File Upload Success Rate
```promql
(rate(cnab_file_uploads_total{status="success"}[5m]) / rate(cnab_file_uploads_total[5m])) * 100
```

### Average File Upload Size
```promql
rate(cnab_file_upload_size_bytes_sum[5m]) / rate(cnab_file_upload_size_bytes_count[5m])
```

### File Upload Processing Duration (P95)
```promql
histogram_quantile(0.95, rate(cnab_file_upload_processing_duration_seconds_bucket[5m]))
```

### Transaction Processing Rate
```promql
rate(cnab_transactions_processed_total[5m])
```

### Transaction Processing Rate by Type and Status
```promql
rate(cnab_transactions_processed_total{type="line", status="success"}[5m])
rate(cnab_transactions_processed_total{type="line", status="failed"}[5m])
rate(cnab_transactions_processed_total{type="line", status="skipped"}[5m])
```

### Transaction Success Rate
```promql
(rate(cnab_transactions_processed_total{status="success"}[5m]) / rate(cnab_transactions_processed_total[5m])) * 100
```

### Current Transactions in Database
```promql
cnab_transactions_in_database
```

### Processing Queue Size
```promql
cnab_processing_queue_size
```

### Queue Operations Rate
```promql
rate(cnab_queue_operations_total[5m])
```

### Queue Operations by Type
```promql
rate(cnab_queue_operations_total{operation="enqueue"}[5m])
rate(cnab_queue_operations_total{operation="dequeue"}[5m])
```

### Processing Errors Rate
```promql
rate(cnab_processing_errors_total[5m])
```

### Processing Errors by Type
```promql
rate(cnab_processing_errors_total{error_type="upload_failed"}[5m])
rate(cnab_processing_errors_total{error_type="line_processing_failed"}[5m])
```

### Processing Errors by Component
```promql
rate(cnab_processing_errors_total{component="FileUploadTrackingService"}[5m])
rate(cnab_processing_errors_total{component="CnabUploadService"}[5m])
```

## Combined Queries

### Total API Operations Rate
```promql
rate(cnab_api_operations_total[5m])
```

### API Operations by Endpoint
```promql
rate(cnab_api_operations_total{endpoint="/transactions/upload"}[5m])
rate(cnab_api_operations_total{endpoint="/transactions/uploads"}[5m])
```

### API Operations by Method
```promql
rate(cnab_api_operations_total{method="POST"}[5m])
rate(cnab_api_operations_total{method="GET"}[5m])
```

### API Operations Success Rate
```promql
(rate(cnab_api_operations_total{status="success"}[5m]) / rate(cnab_api_operations_total[5m])) * 100
```

## Filtering Examples

### Filter by Time Range (Last Hour)
```promql
rate(http_requests_received_total[1h])
```

### Filter by Multiple Status Codes
```promql
rate(http_requests_received_total{code=~"4..|5.."}[5m])  # Client and server errors
```

### Filter by Route Pattern
```promql
rate(http_requests_received_total{route=~"/api/v1/transactions/.*"}[5m])
```

### Exclude Health Check Endpoints
```promql
rate(http_requests_received_total{route!~"/health.*"}[5m])
```

## Aggregation Examples

### Sum of All Uploads
```promql
sum(rate(cnab_file_uploads_total[5m]))
```

### Average Queue Size (Last Hour)
```promql
avg_over_time(cnab_processing_queue_size[1h])
```

### Maximum Response Time (Last Hour)
```promql
max_over_time(histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))[1h])
```

### Top 5 Endpoints by Request Rate
```promql
topk(5, rate(http_requests_received_total[5m]))
```

