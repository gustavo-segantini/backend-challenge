# 📡 API Documentation - CNAB Parser

**Base URL**: `http://localhost:5000/api/v1`  
**Swagger**: `http://localhost:5000/swagger`  
**Version**: v1.0  
**Last Updated**: December 2025

## Table of Contents

1. [MinIO Object Storage](#minio-object-storage)
2. [Authentication](#authentication)
3. [Transactions](#transactions)
4. [Status Codes](#status-codes)
5. [Data Models](#data-models)
6. [Use Case Examples](#use-case-examples)

---

## MinIO Object Storage

### Overview

The application uses **MinIO** as the object storage backend for managing uploaded CNAB files and other file artifacts. MinIO is automatically initialized on application startup using a hosted service pattern that ensures graceful degradation if the storage service is temporarily unavailable.

### Configuration

**Environment Variables** (set in `.env`):
```bash
MINIO_ROOT_USER=cnabuser            # MinIO access credentials
MINIO_ROOT_PASSWORD=cnabpass123     # Secure password (change in production!)
```

**Default Values** (in `docker-compose.yml`):
```yaml
environment:
  MINIO_ROOT_USER: ${MINIO_ROOT_USER:-minioadmin}
  MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD:-minioadmin}
```

### Accessing MinIO Console

The MinIO console provides a web-based interface for managing buckets and files:

**URL**: `http://localhost:9001`  
**Credentials**:
- Username: `cnabuser` (from `.env`)
- Password: `cnabpass123` (from `.env`)

### API Access

MinIO API is available at `http://localhost:9000` (internal Docker) or `http://localhost:9000` (local machine).

### Implementation Details

**Service**: `IObjectStorageService` (implemented by `MinioStorageService`)

**Key Operations**:
- `UploadFileAsync(bucketName, fileName, stream)` - Upload file to MinIO
- `DownloadFileAsync(bucketName, fileName)` - Download file from MinIO
- `DeleteFileAsync(bucketName, fileName)` - Delete file from MinIO

**Usage Example**:
```csharp
// In TransactionFacadeService
await _objectStorageService.UploadFileAsync(
    bucketName: "cnab-uploads",
    fileName: $"cnab-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt",
    stream: fileStream
);
```

### Graceful Degradation

If MinIO is unavailable:
- ✅ Application starts normally
- ✅ Upload endpoints return success (file processing still works)
- ✅ File storage is skipped with logged warning
- ✅ No impact on core transaction processing

**Initialization Service**: `MinioInitializationService` (IHostedService)
- Runs asynchronously during application startup
- Creates default bucket if it doesn't exist
- Non-blocking: doesn't prevent API from starting

### Troubleshooting MinIO

**Check MinIO Health**:
```bash
# From inside Docker
docker-compose exec minio curl -f http://localhost:9000/minio/health/live

# View MinIO logs
docker-compose logs minio
```

**Reset MinIO Data**:
```bash
# Remove MinIO volume and restart
docker-compose down -v
docker-compose up -d --build minio
```

**Connection Issues**:
- Verify `.env` credentials match `docker-compose.yml`
- Ensure MinIO service is healthy: `docker-compose ps`
- Check network connectivity: `docker network inspect cnab_network`

---

## JWT Token Configuration

### Token Expiration & Renewal

The application uses JWT tokens with the following default configuration:

**Tokens Lifetime**:
- **Access Token**: 1440 minutes (24 hours) - Short-lived token for API requests
- **Refresh Token**: 30 days - Long-lived token for obtaining new access tokens

**Configuration** (set in `appsettings.json` or via environment variables):
```json
{
  "Jwt": {
    "Issuer": "cnab-api",
    "Audience": "cnab-api-client",
    "SigningKey": "dev-signing-key-change-me-32-characters-minimum!!",
    "AccessTokenMinutes": 1440,
    "RefreshTokenDays": 30
  }
}
```

**Override via Environment Variables** (in `.env`):
```bash
Jwt__AccessTokenMinutes=1440
Jwt__RefreshTokenDays=30
```

### Token Refresh Flow

When your access token expires, use the refresh token to obtain a new one:

**Endpoint**: `POST /api/v1/auth/refresh`

**Request**:
```json
{
  "refreshToken": "550e8400e29b41d4a716446655440000"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "550e8400e29b41d4a716446655440000",
  "username": "user@example.com",
  "role": "User"
}
```

**Example cURL**:
```bash
curl -X POST http://localhost:5000/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "550e8400e29b41d4a716446655440000"
  }'
```

### Troubleshooting Token Issues

**Error: "IDX10223: Lifetime validation failed. The token is expired"**

This error occurs when:
1. The access token has expired
2. The client is trying to use the expired token instead of refreshing it

**Solution**:
- Always check token expiration before making API requests
- When expired, call the refresh endpoint with your refresh token
- Store refresh tokens securely (recommend HTTP-only cookies in production)

**Error: "Invalid or expired refresh token"**

This occurs when:
1. The refresh token has expired (older than 30 days)
2. The token doesn't exist in the database
3. The refresh token was revoked

**Solution**:
- Re-authenticate the user (login again)
- Implement automatic token refresh on the frontend (before expiration)
- Check that refresh tokens are being stored correctly

### Best Practices

1. **Always implement token refresh logic on the frontend**:
   ```javascript
   // Refresh token before it expires
   async function refreshTokenIfNeeded() {
     const tokenExpiration = getTokenExpiration(); // Parse JWT exp claim
     const now = Date.now();
     
     // Refresh if token expires in less than 5 minutes
     if (tokenExpiration - now < 5 * 60 * 1000) {
       const newToken = await fetch('/api/v1/auth/refresh', {
         method: 'POST',
         body: JSON.stringify({ refreshToken: localStorage.getItem('refreshToken') })
       });
       // Store new tokens
     }
   }
   ```

2. **Store tokens securely**:
   - Use `localStorage` for development (insecure, for demo only)
   - Use `sessionStorage` for temporary session-only tokens
   - Use HTTP-only cookies in production (safer against XSS)

3. **Implement retry logic** with exponential backoff for token refresh failures

4. **Monitor token expiration** and proactively refresh before it expires

5. **In production**, increase the clock skew (`ClockSkew`) to handle clock drift between servers

---

### POST /auth/register

Register a new user on the platform.

**Endpoint**: `POST /api/v1/auth/register`

**Body** (JSON):
```json
{
  "username": "user@example.com",
  "password": "SecurePass123!"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "550e8400e29b41d4a716446655440000",
  "username": "user@example.com",
  "role": "User"
}
```

**Example cURL**:
```bash
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "user@example.com",
    "password": "SecurePass123!"
  }'
```

---

### POST /auth/login

Authenticate user with credentials.

**Endpoint**: `POST /api/v1/auth/login`

**Body** (JSON):
```json
{
  "username": "user@example.com",
  "password": "SecurePass123!"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "550e8400e29b41d4a716446655440000",
  "username": "user@example.com",
  "role": "User"
}
```

---

### GET /auth/github/login

Initiate GitHub authentication flow.

**Endpoint**: `GET /api/v1/auth/github/login?redirectUri=URL`

**Example**:
```bash
curl -X GET "http://localhost:5000/api/v1/auth/github/login?redirectUri=http://localhost:3000/auth"
```

---

### POST /auth/refresh

Renew access token using refresh token.

**Endpoint**: `POST /api/v1/auth/refresh`

**Body** (JSON):
```json
{
  "refreshToken": "550e8400e29b41d4a716446655440000"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "550e8400e29b41d4a716446655440000",
  "username": "user@example.com",
  "role": "User"
}
```

---

### GET /auth/me

Get authenticated user profile.

**Endpoint**: `GET /api/v1/auth/me`

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "username": "user@example.com",
  "role": "User"
}
```

---

### POST /auth/logout

Logout (invalidate refresh token).

**Endpoint**: `POST /api/v1/auth/logout`

**Headers**:
```
Authorization: Bearer {accessToken}
```

**Body** (JSON):
```json
{
  "refreshToken": "550e8400e29b41d4a716446655440000"
}
```

**Response** (200 OK):
```json
{}
```

---

## Transactions

### Asynchronous Processing and Queues

The system uses **Redis Streams** for asynchronous processing of large CNAB files. This enables:

- ✅ **Non-blocking processing**: API returns immediately (202 Accepted)
- ✅ **Horizontal scalability**: Multiple instances can process uploads in parallel
- ✅ **Resilience**: Automatic retry with exponential backoff
- ✅ **Automatic recovery**: Incomplete uploads are detected and re-enqueued
- ✅ **Checkpoints**: Support for resuming processing after failures

#### Queue Architecture

```
┌─────────────────┐
│  API Endpoint   │
│  (Controller)   │
└────────┬────────┘
         │ Enqueue
         ▼
┌─────────────────┐
│  Redis Streams  │
│  (cnab:upload:  │
│   queue)        │
└────────┬────────┘
         │ Dequeue (Consumer Group)
         ▼
┌─────────────────┐
│ Background      │
│ Worker          │
│ (HostedService) │
└────────┬────────┘
         │ Process
         ▼
┌─────────────────┐
│  PostgreSQL     │
│  (Transactions) │
└─────────────────┘
```

#### Dead Letter Queue (DLQ)

Messages that fail after all attempts are moved to the DLQ:

- **Stream**: `cnab:upload:dlq`
- **Content**: UploadId, failure reason, number of attempts
- **Action**: Requires manual intervention or reprocessing process

#### Processing Configuration

Processing options can be configured via `appsettings.json`:

```json
{
  "UploadProcessing": {
    "ParallelWorkers": 4,
    "CheckpointInterval": 100,
    "MaxRetryPerLine": 3,
    "RetryDelayMs": 1000,
    "RecoveryCheckIntervalMinutes": 5,
    "StuckUploadTimeoutMinutes": 30
  }
}
```

**Parameters**:
- `ParallelWorkers`: Number of lines processed in parallel
- `CheckpointInterval`: Lines processed before saving checkpoint
- `MaxRetryPerLine`: Maximum retry attempts per line before failing
- `RetryDelayMs`: Delay between retries (ms)
- `RecoveryCheckIntervalMinutes`: Interval to check for incomplete uploads
- `StuckUploadTimeoutMinutes`: Time to consider upload as stuck

## Transactions

### POST /transactions/upload

Upload and process CNAB file. The file is processed **asynchronously** in the background using Redis Streams.

**Endpoint**: `POST /api/v1/transactions/upload`

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
Content-Type: multipart/form-data
```

**Body** (multipart/form-data):
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| file | file (.txt) | Yes | Formatted CNAB file |

**Expected CNAB Format** (80 characters per line):

Each line contains a transaction with 80 characters in fixed positions:
- Position 0: Transaction type (1 char)
- Position 1-8: Date (YYYYMMDD)
- Position 9-18: Amount (10 digits, last 2 are decimals)
- Position 19-29: CPF (11 characters)
- Position 30-41: Card number (12 characters)
- Position 42-47: Time (HHMMSS)
- Position 48-61: Owner name (14 characters)
- Position 62-79: Store name (18 characters)

**Transaction Types**:
- `1` - Debit (Income)
- `2` - Boleto (Expense)
- `3` - Financing (Expense)
- `4` - Credit (Income)
- `5` - Loan Receipt (Income)
- `6` - Sales (Income)
- `7` - TED Receipt (Income)
- `8` - DOC Receipt (Income)
- `9` - Rent (Expense)

#### Asynchronous Processing Flow

1. **Initial Upload** (Synchronous):
   - File validation (format, size, extension)
   - SHA256 hash calculation for duplicate detection
   - File storage in MinIO (object storage)
   - Creation of `FileUpload` record with `Pending` status
   - Enqueueing to Redis Streams queue

2. **Background Processing** (Asynchronous):
   - Background worker (`UploadProcessingHostedService`) consumes from queue
   - Download file from MinIO
   - Line-by-line parallel processing
   - Periodic checkpoint saving for recovery
   - Status update: `Pending` → `Processing` → `Success`/`Failed`

3. **Automatic Recovery**:
   - `IncompleteUploadRecoveryService` checks for incomplete uploads every 5 minutes
   - Uploads stuck in `Processing` for more than 30 minutes are automatically re-enqueued

**Response (202 Accepted)** - File accepted and enqueued:
```json
{
  "message": "File accepted and queued for background processing",
  "status": "processing"
}
```

**Response (200 OK)** - Synchronous processing (test environment only):
```json
{
  "message": "Successfully imported 100 transactions",
  "count": 100
}
```

**Response (400 Bad Request)**:
```json
{
  "error": "File was not provided or is empty."
}
```

**Response (409 Conflict)** - Duplicate file:
```json
{
  "error": "This file has already been processed previously. To avoid duplicates, the upload was rejected."
}
```

**Example cURL**:
```bash
curl -X POST http://localhost:5000/api/v1/transactions/upload \
  -H "Authorization: Bearer {accessToken}" \
  -F "file=@cnab.txt"
```

#### File Name Format

The file name (`fileName`) saved in the database follows the format `yyyyMMddHHmmss` (UTC date and time of upload).

**Example**: A file uploaded on December 29, 2025 at 14:30:25 UTC will have `fileName` = `"20251229143025"`.

#### Check Upload Status

After receiving `202 Accepted`, you can check the processing status using the upload management endpoints.

---

### GET /transactions/uploads

List all uploads with pagination and optional status filter.

**Endpoint**: `GET /api/v1/transactions/uploads`

**Query Parameters**:
| Parameter | Type | Default | Example | Description |
|-----------|------|---------|---------|-------------|
| page | int | 1 | 2 | Page number (1-based) |
| pageSize | int | 50 | 20 | Items per page (1-100) |
| status | string | - | Processing | Status filter (Pending, Processing, Success, Failed, Duplicate, PartiallyCompleted) |

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "fileName": "20251229143025",
      "status": "Success",
      "fileSize": 10240,
      "totalLineCount": 100,
      "processedLineCount": 100,
      "failedLineCount": 0,
      "skippedLineCount": 0,
      "lastCheckpointLine": 100,
      "lastCheckpointAt": "2025-12-29T10:05:00Z",
      "processingStartedAt": "2025-12-29T10:00:00Z",
      "processingCompletedAt": "2025-12-29T10:05:00Z",
      "uploadedAt": "2025-12-29T10:00:00Z",
      "retryCount": 0,
      "errorMessage": null,
      "storagePath": "cnab-20251229-100000-123.txt",
      "progressPercentage": 100.0
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

**Example cURL**:
```bash
# List all uploads
curl -X GET "http://localhost:5000/api/v1/transactions/uploads?page=1&pageSize=20" \
  -H "Authorization: Bearer {accessToken}"

# Filter by status
curl -X GET "http://localhost:5000/api/v1/transactions/uploads?status=Processing" \
  -H "Authorization: Bearer {accessToken}"
```

**Possible Status Values**:
- `Pending`: File enqueued, awaiting processing
- `Processing`: Being processed by background worker
- `Success`: Processing completed successfully
- `Failed`: Processing failed after all attempts
- `Duplicate`: Duplicate file (already processed previously)
- `PartiallyCompleted`: Processing partially completed (some lines failed)

---

### GET /transactions/uploads/incomplete

List incomplete uploads that are stuck in `Processing` status.

**Endpoint**: `GET /api/v1/transactions/uploads/incomplete`

**Query Parameters**:
| Parameter | Type | Default | Example | Description |
|-----------|------|---------|---------|-------------|
| timeoutMinutes | int | 30 | 60 | Minutos máximos que um upload pode estar em Processing antes de ser considerado travado |

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "incompleteUploads": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "fileName": "20251229143025",
      "status": "Processing",
      "fileSize": 10240,
      "totalLineCount": 100,
      "processedLineCount": 50,
      "failedLineCount": 0,
      "skippedLineCount": 0,
      "lastCheckpointLine": 50,
      "lastCheckpointAt": "2025-12-29T10:02:00Z",
      "processingStartedAt": "2025-12-29T10:00:00Z",
      "processingCompletedAt": null,
      "uploadedAt": "2025-12-29T10:00:00Z",
      "retryCount": 0,
      "errorMessage": null,
      "storagePath": "cnab-20251229-100000-123.txt",
      "progressPercentage": 50.0
    }
  ],
  "count": 1
}
```

**Example cURL**:
```bash
curl -X GET "http://localhost:5000/api/v1/transactions/uploads/incomplete?timeoutMinutes=30" \
  -H "Authorization: Bearer {accessToken}"
```

---

### POST /transactions/uploads/{uploadId}/resume

Resume processing of a specific incomplete upload.

**Endpoint**: `POST /api/v1/transactions/uploads/{uploadId}/resume`

**Path Parameters**:
| Parameter | Type | Required | Example |
|-----------|------|----------|---------|
| uploadId | Guid | Yes | 550e8400-e29b-41d4-a716-446655440000 |

**Headers** (REQUIRED - Admin only):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "message": "Upload re-enqueued for processing",
  "uploadId": "550e8400-e29b-41d4-a716-446655440000",
  "willResumeFromLine": 50,
  "totalLineCount": 100,
  "processedLineCount": 50
}
```

**Response** (400 Bad Request):
```json
{
  "error": "Upload is not incomplete or cannot be resumed"
}
```

**Response** (404 Not Found):
```json
{
  "error": "Upload with ID {uploadId} not found"
}
```

**Example cURL**:
```bash
curl -X POST "http://localhost:5000/api/v1/transactions/uploads/550e8400-e29b-41d4-a716-446655440000/resume" \
  -H "Authorization: Bearer {accessToken}"
```

---

### POST /transactions/uploads/resume-all

Resume processing of all incomplete uploads.

**Endpoint**: `POST /api/v1/transactions/uploads/resume-all`

**Query Parameters**:
| Parameter | Type | Default | Example | Description |
|-----------|------|---------|---------|-------------|
| timeoutMinutes | int | 30 | 60 | Minutos máximos que um upload pode estar em Processing antes de ser considerado travado |

**Headers** (REQUIRED - Admin only):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "message": "Resumed 2 incomplete upload(s)",
  "resumedCount": 2,
  "resumedUploads": [
    {
      "uploadId": "550e8400-e29b-41d4-a716-446655440000",
      "fileName": "file1.txt",
      "willResumeFromLine": 50,
      "totalLineCount": 100,
      "processedLineCount": 50
    },
    {
      "uploadId": "660e8400-e29b-41d4-a716-446655440001",
      "fileName": "file2.txt",
      "willResumeFromLine": 100,
      "totalLineCount": 200,
      "processedLineCount": 100
    }
  ],
  "errors": null
}
```

**Response with Partial Errors** (200 OK):
```json
{
  "message": "Resumed 1 incomplete upload(s)",
  "resumedCount": 1,
  "resumedUploads": [
    {
      "uploadId": "660e8400-e29b-41d4-a716-446655440001",
      "fileName": "file2.txt",
      "willResumeFromLine": 100,
      "totalLineCount": 200,
      "processedLineCount": 100
    }
  ],
  "errors": [
    "Upload 550e8400-e29b-41d4-a716-446655440000 does not have a storage path"
  ]
}
```

**Example cURL**:
```bash
curl -X POST "http://localhost:5000/api/v1/transactions/uploads/resume-all?timeoutMinutes=30" \
  -H "Authorization: Bearer {accessToken}"
```

---

### GET /transactions/{cpf}

List transactions by CPF with pagination, filters, and sorting.

**Endpoint**: `GET /api/v1/transactions/{cpf}`

**Path Parameters**:
| Parameter | Type | Required | Example |
|-----------|------|----------|---------|
| cpf | string | Yes | 09620676017 |

**Query Parameters**:
| Parameter | Type | Default | Example | Description |
|-----------|------|---------|---------|-------------|
| page | int | 1 | 2 | Page number |
| pageSize | int | 50 | 20 | Items per page |
| startDate | datetime | - | 2019-01-01 | Start date filter (ISO 8601) |
| endDate | datetime | - | 2019-12-31 | End date filter (ISO 8601) |
| types | string | - | 1,2,3 | Types separated by comma |
| sort | string | desc | asc | Order: asc (ascending) or desc (descending) |

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": 1,
      "cpf": "09620676017",
      "name": "EMPRESA LTDA",
      "bank": "0001",
      "branch": "0001",
      "account": "1234567",
      "type": 1,
      "nature": "Crédito",
      "value": 1250.50,
      "date": "2019-01-15",
      "time": "23:30:00",
      "storeName": "BAR DO JOÃO"
    }
  ],
  "totalCount": 150,
  "pageSize": 20,
  "currentPage": 1
}
```

**Example cURL**:
```bash
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017?page=1&pageSize=50&sort=desc" \
  -H "Authorization: Bearer {accessToken}"
```

---

### GET /transactions/{cpf}/balance

Calculate total balance for a CPF.

**Endpoint**: `GET /api/v1/transactions/{cpf}/balance`

**Path Parameters**:
| Parameter | Type | Required | Example |
|-----------|------|----------|---------|
| cpf | string | Yes | 09620676017 |

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "balance": 1250.75
}
```

**Balance Calculation**:
- Income transactions (types 1, 4, 5, 6, 7, 8): **+** amount
- Expense transactions (types 2, 3, 9): **-** amount

**Example cURL**:
```bash
curl -X GET http://localhost:5000/api/v1/transactions/09620676017/balance \
  -H "Authorization: Bearer {accessToken}"
```

---

### GET /transactions/{cpf}/search

Search transactions by description (full-text search).

**Endpoint**: `GET /api/v1/transactions/{cpf}/search`

**Query Parameters**:
| Parameter | Type | Required | Example |
|-----------|------|----------|---------|
| searchTerm | string | Yes | LOJA |
| page | int | No | 1 |
| pageSize | int | No | 20 |

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": 1,
      "cpf": "09620676017",
      "name": "LOJA CENTRAL",
      "value": 500.00,
      "date": "2019-01-15"
    }
  ],
  "totalCount": 5,
  "pageSize": 20,
  "currentPage": 1
}
```

**Example cURL**:
```bash
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017/search?searchTerm=LOJA" \
  -H "Authorization: Bearer {accessToken}"
```

---

### GET /transactions/stores/{uploadId}

Get transactions grouped by store name for a specific upload, with balance calculated for each store.

**Endpoint**: `GET /api/v1/transactions/stores/{uploadId}`

**Path Parameters**:
| Parameter | Type | Required | Example |
|-----------|------|----------|---------|
| uploadId | Guid | Yes | 550e8400-e29b-41d4-a716-446655440000 |

**Query Parameters**:
| Parameter | Type | Default | Example | Description |
|-----------|------|---------|---------|-------------|
| page | int | 1 | 2 | Page number (1-based) |
| pageSize | int | 50 | 20 | Items per page (1-100) |

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
```

**Important Note on Grouping**:
- Transactions are grouped **only by `StoreName`** (store name)
- Stores with the same name are grouped together, even if they have different `StoreOwner` values
- The `storeOwner` field in the response shows the first owner found for that store
- The balance (`balance`) is calculated by summing all transactions for the store, regardless of owner

**Response** (200 OK):
```json
{
  "items": [
    {
      "storeName": "BAR DO JOÃO",
      "storeOwner": "096.206.760-17",
      "transactions": [
        {
          "id": 1,
          "storeName": "BAR DO JOÃO",
          "storeOwner": "096.206.760-17",
          "transactionDate": "2019-03-01T00:00:00Z",
          "transactionTime": "15:34:53",
          "amount": 142.00,
          "natureCode": "3"
        },
        {
          "id": 2,
          "storeName": "BAR DO JOÃO",
          "storeOwner": "123.456.789-00",
          "transactionDate": "2019-03-02T10:20:00Z",
          "transactionTime": "10:20:00",
          "amount": 50.00,
          "natureCode": "1"
        }
      ],
      "balance": 92.00
    }
  ],
  "totalCount": 10,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

**Response** (404 Not Found) - No transactions found:
```json
{
  "error": "No transactions found for this upload"
}
```

**Example cURL**:
```bash
# List first page with 20 items per page
curl -X GET "http://localhost:5000/api/v1/transactions/stores/550e8400-e29b-41d4-a716-446655440000?page=1&pageSize=20" \
  -H "Authorization: Bearer {accessToken}"
```

**Note on Pagination**:

This endpoint uses **offset-based pagination** (page-based) instead of cursor-based pagination for the following technical reasons:

1. **Bidirectional Navigation**: Enables direct navigation both forward (Next) and backward (Previous) without requiring client-side state management. The frontend can easily calculate previous pages using `page - 1`.

2. **Total Count Availability**: Provides `totalCount` and `totalPages`, enabling the frontend to display information like "Page 2 of 5" and implement direct navigation to specific pages (e.g., go to page 3).

3. **Data Consistency**: For the store-grouped transaction use case, where data is relatively stable after CNAB file processing, offset-based pagination provides a consistent snapshot of data at query time, with minimal risk of duplicate or missing items during navigation.

4. **Use Case Alignment**: Since transactions are grouped by store and data doesn't change frequently during viewing (file already processed), there's no need for cursor-based pagination, which is more suitable for constantly changing data or extremely large volumes (millions of records).

5. **Frontend Implementation Simplicity**: The interface can implement traditional pagination controls (Previous/Next buttons, page selector) without needing to manage cursor tokens or additional state.

6. **Stable Ordering**: Ordering by `StoreName` is stable and predictable, ensuring that the same page always returns the same results when queried, as long as data hasn't been modified.

---

### DELETE /transactions

Clear all transactions (Admin only).

**Endpoint**: `DELETE /api/v1/transactions`

**Headers** (REQUIRED):
```
Authorization: Bearer {accessToken}
```

**Authorization**: Requires `Admin` role

**Response** (200 OK):
```json
{
  "message": "All data cleared successfully"
}
```

**Example cURL**:
```bash
curl -X DELETE http://localhost:5000/api/v1/transactions \
  -H "Authorization: Bearer {accessToken}"
```

---

## Status Codes

| Status | Description | Example |
|--------|-------------|---------|
| 200 | OK - Success | Transactions returned |
| 302 | Found - Redirect | OAuth GitHub |
| 400 | Bad Request - Validation error | Invalid CPF |
| 401 | Unauthorized - No authentication | Missing token |
| 403 | Forbidden - No authorization | Not Admin |
| 500 | Internal Server Error | Server error |

---

## Data Models

### AuthResponse
```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "username": "string",
  "role": "User | Admin"
}
```

### Transaction
```json
{
  "id": "integer",
  "cpf": "string (11 chars)",
  "name": "string",
  "bank": "string",
  "branch": "string",
  "account": "string",
  "type": "integer (1-9)",
  "nature": "string",
  "value": "decimal",
  "date": "string (YYYY-MM-DD)",
  "time": "string (HH:mm:ss)",
  "storeName": "string"
}
```

### PagedResult<T>
```json
{
  "items": "Array<T>",
  "totalCount": "integer",
  "pageSize": "integer",
  "currentPage": "integer"
}
```

---

## Use Case Examples

### 1️⃣ Complete Flow: Login → Upload → Query

```bash
# 1. Login
TOKEN=$(curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "user@example.com",
    "password": "SecurePass123!"
  }' | jq -r '.accessToken')

# 2. Upload CNAB
curl -X POST http://localhost:5000/api/v1/transactions/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@cnab.txt"

# 3. Query transactions
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017?page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"

# 4. Get balance
curl -X GET http://localhost:5000/api/v1/transactions/09620676017/balance \
  -H "Authorization: Bearer $TOKEN"
```

### 2️⃣ Renew Expired Token

```javascript
async function refreshToken() {
  const response = await fetch('/api/v1/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ 
      refreshToken: localStorage.getItem('refreshToken')
    })
  });
  
  if (response.ok) {
    const data = await response.json();
    localStorage.setItem('accessToken', data.accessToken);
    return true;
  }
  return false;
}
```

### 3️⃣ Filter Transactions by Date

```bash
# Credit transactions (type 1) in 2019
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017?startDate=2019-01-01&endDate=2019-12-31&types=1&sort=desc" \
  -H "Authorization: Bearer $TOKEN"

# Last 5 transactions
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017?page=1&pageSize=5&sort=desc" \
  -H "Authorization: Bearer $TOKEN"
```

### 4️⃣ Search by Store

```bash
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017/search?searchTerm=LOJA" \
  -H "Authorization: Bearer $TOKEN"
```

---

**Last Updated**: December 29, 2025  
**Version**: v1.1.0

// Upload CNAB file
export const uploadCnabFile = async (file) => {
  const formData = new FormData();
  formData.append('file', file);
  
  const response = await axios.post(
    `${API_BASE_URL}/transactions/upload`,
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' }
    }
  );
  
  return response.data;
};

// Get transactions by CPF
export const getTransactionsByCpf = async (cpf) => {
  const response = await axios.get(
    `${API_BASE_URL}/transactions/${cpf}`
  );
  return response.data;
};

// Get balance by CPF
export const getBalanceByCpf = async (cpf) => {
  const response = await axios.get(
    `${API_BASE_URL}/transactions/${cpf}/balance`
  );
  return response.data;
};

// Clear all data
export const clearAllData = async () => {
  const response = await axios.delete(
    `${API_BASE_URL}/transactions`
  );
  return response.data;
};
```

---

## Database Schema

### Transaction Table
```sql
CREATE TABLE "Transactions" (
    "Id" SERIAL PRIMARY KEY,
    "BankCode" VARCHAR(4) NOT NULL,
    "Cpf" VARCHAR(11) NOT NULL,
    "NatureCode" VARCHAR(12) NOT NULL,
    "Amount" DECIMAL(18,2) NOT NULL,
    "Card" VARCHAR(12) NOT NULL,
    "StoreOwner" VARCHAR(14) NOT NULL,
    "StoreName" VARCHAR(18) NOT NULL,
    "TransactionDate" TIMESTAMPTZ NOT NULL,
    "TransactionTime" INTERVAL NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_transactions_cpf ON "Transactions"("Cpf");
CREATE INDEX idx_transactions_date ON "Transactions"("TransactionDate" DESC);
```

---

## Swagger UI

Access interactive API documentation at:
```
http://localhost:5000/swagger
```

The Swagger UI provides:
- Interactive endpoint testing
- Request/response schemas
- Try-it-out functionality
- Model definitions

---

## Rate Limiting & Performance

The API implements **IP-based rate limiting** to protect against abuse and ensure fair usage.

### Rate Limit Rules

| Endpoint | Limit | Period | Description |
|----------|-------|--------|-------------|
| All endpoints | 100 requests | 1 minute | General API limit |
| All endpoints | 1000 requests | 1 hour | Hourly API limit |
| `POST /transactions/upload` | 60 requests | 1 minute | File upload limit (1 req/sec) |
| `POST /auth/login` | 5 requests | 1 minute | Login attempt limit |
| `POST /auth/register` | 3 requests | 1 hour | Registration limit |

### Rate Limit Headers

When rate limiting is active, the API returns the following headers:

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1640995200
```

### Rate Limit Exceeded Response

When the rate limit is exceeded, the API returns:

**Status Code**: `429 Too Many Requests`

**Response Body**:
```json
{
  "error": "API rate limit exceeded. Maximum 100 requests per 1m."
}
```

### Whitelisted Endpoints

The following endpoints are **excluded** from rate limiting:
- `GET /api/v1/health`
- `GET /api/v1/health/ready`
- `GET /api/v1/health/live`

### Best Practices

- **Upload files**: Keep file size reasonable (< 10MB recommended)
- **Batch operations**: Use pagination for large result sets
- **Respect rate limits**: Implement exponential backoff when receiving 429 responses
- **Monitor usage**: Check `X-RateLimit-Remaining` header to track remaining requests

---

## Security Considerations

**Current Implementation:**
- No authentication/authorization
- CORS enabled for development (configured for `http://localhost:3000`)
- File upload validation (extension, size, format)

**Future Enhancements:**
- JWT authentication
- Role-based access control
- API rate limiting
- File upload virus scanning

---

## Support & Troubleshooting

### Common Issues

**1. File upload fails with "File content is empty"**
- Ensure the file is not empty
- Check file encoding (UTF-8 recommended)
- Verify file format matches CNAB specification

**2. "Invalid line: expected minimum 80 characters"**
- Check that each line has exactly 80 characters
- Remove trailing newlines or spaces
- Verify line endings (LF or CRLF)

**3. Empty results when querying by CPF**
- Verify CPF format (11 digits)
- Check if CPF exists in uploaded file
- Ensure transactions were successfully imported

**4. Balance calculation seems incorrect**
- Verify transaction types in source file
- Check if all transactions were imported
- Review signed amount calculation logic

---

## Testing & Quality

### Code Coverage

The project maintains high test coverage to ensure quality and reliability:

| Metric | Value | Status |
|--------|-------|--------|
| **Line Coverage** | 89.94% | ✅ Excellent |
| **Branch Coverage** | 78.09% | ✅ Very Good |
| **Method Coverage** | 93.67% | ✅ Excellent |
| **Total Tests** | 603 | - |
| **Tests Passed** | 603 | ✅ |
| **Tests Failed** | 0 | ✅ |
| **Tests Ignored** | 0 | ✅ |

### Running Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test backend.Tests/CnabApi.Tests.csproj

# Integration tests only
dotnet test backend.IntegrationTests/CnabApi.IntegrationTests.csproj

# With coverage report
dotnet test backend.Tests/CnabApi.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

### Test Quality Improvements

**Optimized Structure:**
- ✅ **Consolidated tests**: Duplicate tests were merged into `[Theory]` with `[InlineData]` to reduce duplication
- ✅ **Removed tests**: Tests marked as `Skip` that cannot be executed were removed
- ✅ **Expanded coverage**: Added tests for previously uncovered methods

**New Tests Created:**
- `HashServiceTests`: Complete tests for ComputeFileHash, ComputeLineHash, ComputeStreamHashAsync
- `FileUploadTrackingServiceTests`: Tests for CommitLineHashesAsync, FindIncompleteUploadsAsync, UpdateProcessingResultAsync
- `TransactionServiceTests`: Tests for AddSingleTransactionAsync, AddTransactionToContextAsync
- `CnabParserServiceTests`: Consolidated tests for parsing different fields
- `EfCoreUnitOfWorkTests`: Complete tests for transaction management
- `LineProcessorTests`: Tests for line processing with various scenarios
- `CheckpointManagerTests`: Tests for checkpoint logic
- `UploadStatusCodeStrategyFactoryTests`: Tests for status code determination
- `AsynchronousUploadProcessingStrategyTests`: Complete tests for asynchronous upload processing strategy (enqueueing, error handling, cancellation)
- `SynchronousUploadProcessingStrategyTests`: Complete tests for synchronous upload processing strategy (immediate processing, success/failure scenarios, exception handling)
- `UploadManagementServiceTests`: Complete tests for upload management operations (queries with pagination and filters, incomplete uploads detection, resume operations with validation and error handling)

### Generate Coverage Report

```bash
# 1. Run tests with coverage
dotnet test backend.Tests/CnabApi.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# 2. Generate HTML report (requires reportgenerator)
reportgenerator -reports:backend.Tests/coverage.cobertura.xml -targetdir:backend.Tests/TestResults/CoverageReport -reporttypes:Html

# 3. View report
start backend.Tests/TestResults/CoverageReport/index.html  # Windows
```

### Install ReportGenerator

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### Coverage Exclusions

Infrastructure code excluded from coverage (marked with `[ExcludeFromCodeCoverage]`):
- ✅ Entity Framework Core Migrations
- ✅ Program.cs (startup configuration)
- ✅ Configuration extensions (DI, Middleware, HealthChecks)
- ✅ DataSeeder (initial data)
- ✅ Global exception middleware
- ✅ Redis services (RedisDistributedLockService, RedisUploadQueueService) - require integration tests with Redis
- ✅ MinIO services (MinioInitializationService, MinioStorageService, MinioStorageConfiguration) - require integration tests with MinIO
- ✅ Test infrastructure (MockDistributedLockService, MockUploadQueueService) - not part of business logic

This ensures that metrics reflect only **testable business code**. Infrastructure components that require external services (Redis, MinIO) are excluded and should be tested with integration tests.

### Test Quality Improvements

**Test Consolidation:**
- ✅ Duplicate tests were consolidated into `[Theory]` with `[InlineData]` for better maintainability
- ✅ Removed tests that cannot be executed (marked as Skip)
- ✅ Added comprehensive tests for previously uncovered methods

**Coverage by Module:**
- ✅ **HashService**: 100% coverage (ComputeFileHash, ComputeLineHash, ComputeStreamHashAsync)
- ✅ **FileUploadTrackingService**: Complete coverage including CommitLineHashesAsync, FindIncompleteUploadsAsync, UpdateProcessingResultAsync
- ✅ **CnabParserService**: Consolidated tests for parsing different fields
- ✅ **TransactionService**: Tests for AddSingleTransactionAsync and AddTransactionToContextAsync
- ✅ **FileService**: Consolidated tests for extension and content validation
- ✅ **UnitOfWork**: Complete tests for transaction management (with InMemory warnings suppression)
- ✅ **AsynchronousUploadProcessingStrategy**: Complete coverage for enqueueing, error handling, and cancellation scenarios
- ✅ **SynchronousUploadProcessingStrategy**: Complete coverage for immediate processing, success/failure scenarios, and exception handling
- ✅ **UploadManagementService**: Complete coverage for upload queries, incomplete uploads detection, and resume operations with validation

---

## Versioning

**Current Version:** 1.0.0

**Changelog:**
- v1.0.0 (2025-12-21): Initial release
  - CNAB file upload
  - Transaction query by CPF
  - Balance calculation
  - Data clearing functionality
  - Store information support
  - 80.35% test coverage (370 tests)
- v1.1.0 (2025-12-29): Test Quality Improvements
  - Increased test count from 370 to 546 tests
  - Consolidated duplicate tests into `[Theory]` tests with `[InlineData]`
  - Added comprehensive tests for previously uncovered methods
  - Removed tests that cannot be executed (Skip tests)
  - Marked infrastructure services as `[ExcludeFromCodeCoverage]` (Redis, MinIO, Mock services)
  - Current coverage: 80.15% line, 70.13% branch, 88.53% method (546 tests)
- v1.2.0 (2025-12-29): Upload Processing Strategy Tests
  - Added comprehensive tests for `AsynchronousUploadProcessingStrategy` (enqueueing, error handling, cancellation)
  - Added comprehensive tests for `SynchronousUploadProcessingStrategy` (immediate processing, success/failure scenarios, exception handling)
  - Increased test count from 546 to 585 tests
  - Current coverage: 85.98% line, 76.35% branch, 90.06% method (585 tests)
- v1.3.0 (2025-12-29): Upload Management Service Tests
  - Added comprehensive tests for `UploadManagementService` (queries with pagination and filters, incomplete uploads detection, resume operations)
  - Increased test count from 585 to 603 tests
  - Current coverage: 89.94% line, 78.09% branch, 93.67% method (603 tests)

---

## Contact & Contributing

For questions, bug reports, or feature requests, please contact the development team.

---

*Last Updated: December 29, 2025*
