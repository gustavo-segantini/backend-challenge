# 📡 API Documentation - CNAB Parser

**Base URL**: `http://localhost:5000/api/v1`  
**Swagger**: `http://localhost:5000/swagger`  
**Version**: v1.0  
**Last Updated**: December 2025

## Índice

1. [MinIO Object Storage](#minio-object-storage)
2. [Autenticação](#autenticação)
3. [Transações](#transações)
4. [Códigos de Status](#códigos-de-status)
5. [Modelos de Dados](#modelos-de-dados)
6. [Exemplos por Caso de Uso](#exemplos-por-caso-de-uso)

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

Registrar novo usuário na plataforma.

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

**Exemplo cURL**:
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

Autenticar usuário com credenciais.

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

Iniciar fluxo de autenticação com GitHub.

**Endpoint**: `GET /api/v1/auth/github/login?redirectUri=URL`

**Exemplo**:
```bash
curl -X GET "http://localhost:5000/api/v1/auth/github/login?redirectUri=http://localhost:3000/auth"
```

---

### POST /auth/refresh

Renovar access token usando refresh token.

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

Obter perfil do usuário autenticado.

**Endpoint**: `GET /api/v1/auth/me`

**Headers** (OBRIGATÓRIO):
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

Fazer logout (invalidar refresh token).

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

## Transações

### Processamento Assíncrono e Filas

O sistema utiliza **Redis Streams** para processamento assíncrono de arquivos CNAB grandes. Isso permite:

- ✅ **Processamento não-bloqueante**: API retorna imediatamente (202 Accepted)
- ✅ **Escalabilidade horizontal**: Múltiplas instâncias podem processar uploads em paralelo
- ✅ **Resiliência**: Retry automático com exponential backoff
- ✅ **Recuperação automática**: Uploads incompletos são detectados e re-enfileirados
- ✅ **Checkpoints**: Suporte a retomada de processamento após falhas

#### Arquitetura de Filas

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

Mensagens que falham após todas as tentativas são movidas para a DLQ:

- **Stream**: `cnab:upload:dlq`
- **Conteúdo**: UploadId, motivo da falha, número de tentativas
- **Ação**: Requer intervenção manual ou processo de reprocessamento

#### Configuração de Processamento

As opções de processamento podem ser configuradas via `appsettings.json`:

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

**Parâmetros**:
- `ParallelWorkers`: Número de linhas processadas em paralelo
- `CheckpointInterval`: Linhas processadas antes de salvar checkpoint
- `MaxRetryPerLine`: Tentativas máximas por linha antes de falhar
- `RetryDelayMs`: Delay entre tentativas (ms)
- `RecoveryCheckIntervalMinutes`: Intervalo para verificar uploads incompletos
- `StuckUploadTimeoutMinutes`: Tempo para considerar upload como travado

## Transações

### POST /transactions/upload

Fazer upload e processar arquivo CNAB. O arquivo é processado de forma **assíncrona** em background usando Redis Streams.

**Endpoint**: `POST /api/v1/transactions/upload`

**Headers** (OBRIGATÓRIO):
```
Authorization: Bearer {accessToken}
Content-Type: multipart/form-data
```

**Body** (multipart/form-data):
| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-----------|-----------|
| file | file (.txt) | Sim | Arquivo CNAB formatado |

**Formato CNAB esperado** (80 caracteres por linha):

Cada linha contém uma transação com 80 caracteres em posições fixas:
- Posição 0: Tipo de transação (1 char)
- Posição 1-8: Data (YYYYMMDD)
- Posição 9-18: Valor (10 dígitos, últimos 2 são decimais)
- Posição 19-29: CPF (11 caracteres)
- Posição 30-41: Número do cartão (12 caracteres)
- Posição 42-47: Hora (HHMMSS)
- Posição 48-61: Nome do proprietário (14 caracteres)
- Posição 62-79: Nome da loja (18 caracteres)

**Tipos de transação**:
- `1` - Débito (Entrada)
- `2` - Boleto (Saída)
- `3` - Financiamento (Saída)
- `4` - Crédito (Entrada)
- `5` - Recebimento de Empréstimo (Entrada)
- `6` - Vendas (Entrada)
- `7` - Recebimento TED (Entrada)
- `8` - Recebimento DOC (Entrada)
- `9` - Aluguel (Saída)

#### Fluxo de Processamento Assíncrono

1. **Upload Inicial** (Síncrono):
   - Validação do arquivo (formato, tamanho, extensão)
   - Cálculo de hash SHA256 para detecção de duplicatas
   - Armazenamento do arquivo no MinIO (object storage)
   - Criação do registro `FileUpload` com status `Pending`
   - Enfileiramento na fila Redis Streams

2. **Processamento em Background** (Assíncrono):
   - Worker background (`UploadProcessingHostedService`) consome da fila
   - Download do arquivo do MinIO
   - Processamento linha por linha em paralelo
   - Salvamento de checkpoints periódicos para recuperação
   - Atualização do status: `Pending` → `Processing` → `Success`/`Failed`

3. **Recuperação Automática**:
   - Serviço `IncompleteUploadRecoveryService` verifica uploads incompletos a cada 5 minutos
   - Uploads travados em `Processing` por mais de 30 minutos são re-enfileirados automaticamente

**Response (202 Accepted)** - Arquivo aceito e enfileirado:
```json
{
  "message": "File accepted and queued for background processing",
  "status": "processing"
}
```

**Response (200 OK)** - Processamento síncrono (apenas em ambiente de teste):
```json
{
  "message": "Successfully imported 100 transactions",
  "count": 100
}
```

**Response (400 Bad Request)**:
```json
{
  "error": "Arquivo não foi fornecido ou está vazio."
}
```

**Response (409 Conflict)** - Arquivo duplicado:
```json
{
  "error": "Este arquivo já foi processado anteriormente. Para evitar duplicatas, o upload foi rejeitado."
}
```

**Exemplo cURL**:
```bash
curl -X POST http://localhost:5000/api/v1/transactions/upload \
  -H "Authorization: Bearer {accessToken}" \
  -F "file=@cnab.txt"
```

#### Formato do Nome do Arquivo

O nome do arquivo (`fileName`) salvo no banco de dados segue o formato `yyyyMMddHHmmss` (data e hora UTC do upload).

**Exemplo**: Um arquivo enviado em 29 de dezembro de 2025 às 14:30:25 UTC terá `fileName` = `"20251229143025"`.

#### Verificar Status do Upload

Após receber `202 Accepted`, você pode verificar o status do processamento usando os endpoints de gerenciamento de uploads.

---

### GET /transactions/uploads

Lista todos os uploads com paginação e filtro opcional por status.

**Endpoint**: `GET /api/v1/transactions/uploads`

**Query Parameters**:
| Parâmetro | Tipo | Padrão | Exemplo | Descrição |
|-----------|------|-------|---------|-----------|
| page | int | 1 | 2 | Número da página (1-based) |
| pageSize | int | 50 | 20 | Itens por página (1-100) |
| status | string | - | Processing | Filtro por status (Pending, Processing, Success, Failed, Duplicate, PartiallyCompleted) |

**Headers** (OBRIGATÓRIO):
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

**Exemplo cURL**:
```bash
# Listar todos os uploads
curl -X GET "http://localhost:5000/api/v1/transactions/uploads?page=1&pageSize=20" \
  -H "Authorization: Bearer {accessToken}"

# Filtrar por status
curl -X GET "http://localhost:5000/api/v1/transactions/uploads?status=Processing" \
  -H "Authorization: Bearer {accessToken}"
```

**Status possíveis**:
- `Pending`: Arquivo enfileirado, aguardando processamento
- `Processing`: Sendo processado por worker background
- `Success`: Processamento concluído com sucesso
- `Failed`: Processamento falhou após todas as tentativas
- `Duplicate`: Arquivo duplicado (já foi processado anteriormente)
- `PartiallyCompleted`: Processamento parcialmente concluído (algumas linhas falharam)

---

### GET /transactions/uploads/incomplete

Lista uploads incompletos que estão travados em status `Processing`.

**Endpoint**: `GET /api/v1/transactions/uploads/incomplete`

**Query Parameters**:
| Parâmetro | Tipo | Padrão | Exemplo | Descrição |
|-----------|------|-------|---------|-----------|
| timeoutMinutes | int | 30 | 60 | Minutos máximos que um upload pode estar em Processing antes de ser considerado travado |

**Headers** (OBRIGATÓRIO):
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

**Exemplo cURL**:
```bash
curl -X GET "http://localhost:5000/api/v1/transactions/uploads/incomplete?timeoutMinutes=30" \
  -H "Authorization: Bearer {accessToken}"
```

---

### POST /transactions/uploads/{uploadId}/resume

Retoma o processamento de um upload incompleto específico.

**Endpoint**: `POST /api/v1/transactions/uploads/{uploadId}/resume`

**Path Parameters**:
| Parâmetro | Tipo | Obrigatório | Exemplo |
|-----------|------|-----------|---------|
| uploadId | Guid | Sim | 550e8400-e29b-41d4-a716-446655440000 |

**Headers** (OBRIGATÓRIO - Admin apenas):
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

**Exemplo cURL**:
```bash
curl -X POST "http://localhost:5000/api/v1/transactions/uploads/550e8400-e29b-41d4-a716-446655440000/resume" \
  -H "Authorization: Bearer {accessToken}"
```

---

### POST /transactions/uploads/resume-all

Retoma o processamento de todos os uploads incompletos.

**Endpoint**: `POST /api/v1/transactions/uploads/resume-all`

**Query Parameters**:
| Parâmetro | Tipo | Padrão | Exemplo | Descrição |
|-----------|------|-------|---------|-----------|
| timeoutMinutes | int | 30 | 60 | Minutos máximos que um upload pode estar em Processing antes de ser considerado travado |

**Headers** (OBRIGATÓRIO - Admin apenas):
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

**Response com erros parciais** (200 OK):
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

**Exemplo cURL**:
```bash
curl -X POST "http://localhost:5000/api/v1/transactions/uploads/resume-all?timeoutMinutes=30" \
  -H "Authorization: Bearer {accessToken}"
```

---

### GET /transactions/{cpf}

Listar transações por CPF com paginação, filtros e ordenação.

**Endpoint**: `GET /api/v1/transactions/{cpf}`

**Path Parameters**:
| Parâmetro | Tipo | Obrigatório | Exemplo |
|-----------|------|-----------|---------|
| cpf | string | Sim | 09620676017 |

**Query Parameters**:
| Parâmetro | Tipo | Padrão | Exemplo | Descrição |
|-----------|------|-------|---------|-----------|
| page | int | 1 | 2 | Número da página |
| pageSize | int | 50 | 20 | Itens por página |
| startDate | datetime | - | 2019-01-01 | Filtro data início (ISO 8601) |
| endDate | datetime | - | 2019-12-31 | Filtro data fim (ISO 8601) |
| types | string | - | 1,2,3 | Tipos separados por vírgula |
| sort | string | desc | asc | Ordem: asc (crescente) ou desc (decrescente) |

**Headers** (OBRIGATÓRIO):
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

**Exemplo cURL**:
```bash
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017?page=1&pageSize=50&sort=desc" \
  -H "Authorization: Bearer {accessToken}"
```

---

### GET /transactions/{cpf}/balance

Calcular saldo total para um CPF.

**Endpoint**: `GET /api/v1/transactions/{cpf}/balance`

**Path Parameters**:
| Parâmetro | Tipo | Obrigatório | Exemplo |
|-----------|------|-----------|---------|
| cpf | string | Sim | 09620676017 |

**Headers** (OBRIGATÓRIO):
```
Authorization: Bearer {accessToken}
```

**Response** (200 OK):
```json
{
  "balance": 1250.75
}
```

**Cálculo do saldo**:
- Transações de entrada (tipos 1, 4, 5, 6, 7, 8): **+** valor
- Transações de saída (tipos 2, 3, 9): **-** valor

**Exemplo cURL**:
```bash
curl -X GET http://localhost:5000/api/v1/transactions/09620676017/balance \
  -H "Authorization: Bearer {accessToken}"
```

---

### GET /transactions/{cpf}/search

Buscar transações por descrição (full-text search).

**Endpoint**: `GET /api/v1/transactions/{cpf}/search`

**Query Parameters**:
| Parâmetro | Tipo | Obrigatório | Exemplo |
|-----------|------|-----------|---------|
| searchTerm | string | Sim | LOJA |
| page | int | Não | 1 |
| pageSize | int | Não | 20 |

**Headers** (OBRIGATÓRIO):
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

**Exemplo cURL**:
```bash
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017/search?searchTerm=LOJA" \
  -H "Authorization: Bearer {accessToken}"
```

---

### GET /transactions/stores/{uploadId}

Obter transações agrupadas por nome da loja para um upload específico, com saldo calculado para cada loja.

**Endpoint**: `GET /api/v1/transactions/stores/{uploadId}`

**Path Parameters**:
| Parâmetro | Tipo | Obrigatório | Exemplo |
|-----------|------|-----------|---------|
| uploadId | Guid | Sim | 550e8400-e29b-41d4-a716-446655440000 |

**Headers** (OBRIGATÓRIO):
```
Authorization: Bearer {accessToken}
```

**Nota importante sobre agrupamento**:
- As transações são agrupadas **apenas por `StoreName`** (nome da loja)
- Lojas com o mesmo nome são agrupadas juntas, mesmo que tenham `StoreOwner` diferentes
- O campo `storeOwner` na resposta mostra o primeiro proprietário encontrado para aquela loja
- O saldo (`balance`) é calculado somando todas as transações da loja, independente do proprietário

**Response** (200 OK):
```json
[
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
]
```

**Response** (404 Not Found) - Nenhuma transação encontrada:
```json
{
  "error": "No transactions found for this upload"
}
```

**Exemplo cURL**:
```bash
curl -X GET "http://localhost:5000/api/v1/transactions/stores/550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer {accessToken}"
```

---

### DELETE /transactions

Limpar todas as transações (apenas Admin).

**Endpoint**: `DELETE /api/v1/transactions`

**Headers** (OBRIGATÓRIO):
```
Authorization: Bearer {accessToken}
```

**Autorização**: Requer role `Admin`

**Response** (200 OK):
```json
{
  "message": "All data cleared successfully"
}
```

**Exemplo cURL**:
```bash
curl -X DELETE http://localhost:5000/api/v1/transactions \
  -H "Authorization: Bearer {accessToken}"
```

---

## Códigos de Status

| Status | Descrição | Exemplo |
|--------|-----------|---------|
| 200 | OK - Sucesso | Transações retornadas |
| 302 | Found - Redirecionamento | OAuth GitHub |
| 400 | Bad Request - Erro de validação | CPF inválido |
| 401 | Unauthorized - Sem autenticação | Token ausente |
| 403 | Forbidden - Sem autorização | Não é Admin |
| 500 | Internal Server Error | Erro no servidor |

---

## Modelos de Dados

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

## Exemplos por Caso de Uso

### 1️⃣ Fluxo Completo: Login → Upload → Consultar

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

# 3. Consultar transações
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017?page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"

# 4. Obter saldo
curl -X GET http://localhost:5000/api/v1/transactions/09620676017/balance \
  -H "Authorization: Bearer $TOKEN"
```

### 2️⃣ Renovar Token Expirado

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

### 3️⃣ Filtrar Transações por Data

```bash
# Transações de crédito (tipo 1) em 2019
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017?startDate=2019-01-01&endDate=2019-12-31&types=1&sort=desc" \
  -H "Authorization: Bearer $TOKEN"

# Últimas 5 transações
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017?page=1&pageSize=5&sort=desc" \
  -H "Authorization: Bearer $TOKEN"
```

### 4️⃣ Buscar por Loja

```bash
curl -X GET "http://localhost:5000/api/v1/transactions/09620676017/search?searchTerm=LOJA" \
  -H "Authorization: Bearer $TOKEN"
```

---

**Última atualização**: Dezembro 29, 2025  
**Versão**: v1.1.0

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

Current implementation has no rate limiting. Recommended practices:
- Upload files with reasonable size (< 10MB)
- Batch operations for large datasets
- Use pagination for large result sets (future enhancement)

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

**1. File upload fails with "Conteúdo do arquivo está vazio"**
- Ensure the file is not empty
- Check file encoding (UTF-8 recommended)
- Verify file format matches CNAB specification

**2. "Linha inválida: esperado mínimo 80 caracteres"**
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

## Testes e Qualidade

### Code Coverage

O projeto mantém alta cobertura de testes para garantir qualidade e confiabilidade:

| Métrica | Valor | Status |
|---------|-------|--------|
| **Line Coverage** | 80.15% | ✅ Excelente |
| **Branch Coverage** | 70.13% | ✅ Muito Bom |
| **Method Coverage** | 88.53% | ✅ Excelente |
| **Total de Testes** | 546 | - |
| **Testes Aprovados** | 546 | ✅ |
| **Testes Falhando** | 0 | ✅ |
| **Testes Ignorados** | 0 | ✅ |

### Executar Testes

```bash
# Todos os testes
dotnet test

# Apenas unitários
dotnet test backend.Tests/CnabApi.Tests.csproj

# Apenas integração
dotnet test backend.IntegrationTests/CnabApi.IntegrationTests.csproj

# Com relatório de cobertura
dotnet test backend.Tests/CnabApi.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

### Melhorias na Qualidade dos Testes

**Estrutura Otimizada:**
- ✅ **Testes consolidados**: Testes duplicados foram mesclados em `[Theory]` com `[InlineData]` para reduzir duplicação
- ✅ **Testes removidos**: Testes marcados como `Skip` que não podem ser executados foram removidos
- ✅ **Cobertura expandida**: Adicionados testes para métodos anteriormente não cobertos

**Novos Testes Criados:**
- `HashServiceTests`: Testes completos para ComputeFileHash, ComputeLineHash, ComputeStreamHashAsync
- `FileUploadTrackingServiceTests`: Testes para CommitLineHashesAsync, FindIncompleteUploadsAsync, UpdateProcessingResultAsync
- `TransactionServiceTests`: Testes para AddSingleTransactionAsync, AddTransactionToContextAsync
- `CnabParserServiceTests`: Testes consolidados para parsing de diferentes campos
- `EfCoreUnitOfWorkTests`: Testes completos para gerenciamento de transações
- `LineProcessorTests`: Testes para processamento de linhas com vários cenários
- `CheckpointManagerTests`: Testes para lógica de checkpoint
- `UploadStatusCodeStrategyFactoryTests`: Testes para determinação de códigos de status

### Gerar Relatório de Cobertura

```bash
# 1. Executar testes com cobertura
dotnet test backend.Tests/CnabApi.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# 2. Gerar relatório HTML (requer reportgenerator)
reportgenerator -reports:backend.Tests/coverage.cobertura.xml -targetdir:backend.Tests/TestResults/CoverageReport -reporttypes:Html

# 3. Visualizar relatório
start backend.Tests/TestResults/CoverageReport/index.html  # Windows
```

### Instalar ReportGenerator

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### Exclusões de Coverage

Código de infraestrutura excluído da cobertura (marcado com `[ExcludeFromCodeCoverage]`):
- ✅ Migrations do Entity Framework Core
- ✅ Program.cs (configuração de startup)
- ✅ Extensions de configuração (DI, Middleware, HealthChecks)
- ✅ DataSeeder (dados iniciais)
- ✅ Middleware global de exceções
- ✅ Serviços Redis (RedisDistributedLockService, RedisUploadQueueService) - requerem testes de integração com Redis
- ✅ Serviços MinIO (MinioInitializationService, MinioStorageService, MinioStorageConfiguration) - requerem testes de integração com MinIO
- ✅ Infraestrutura de testes (MockDistributedLockService, MockUploadQueueService) - não fazem parte da lógica de negócio

Isso garante que as métricas refletem apenas **código de negócio testável**. Componentes de infraestrutura que requerem serviços externos (Redis, MinIO) são excluídos e devem ser testados com testes de integração.

### Melhorias na Qualidade dos Testes

**Consolidação de Testes:**
- ✅ Testes duplicados foram consolidados em `[Theory]` com `[InlineData]` para melhor manutenibilidade
- ✅ Removidos testes que não podem ser executados (marcados como Skip)
- ✅ Adicionados testes abrangentes para métodos anteriormente não cobertos

**Cobertura por Módulo:**
- ✅ **HashService**: 100% cobertura (ComputeFileHash, ComputeLineHash, ComputeStreamHashAsync)
- ✅ **FileUploadTrackingService**: Cobertura completa incluindo CommitLineHashesAsync, FindIncompleteUploadsAsync, UpdateProcessingResultAsync
- ✅ **CnabParserService**: Testes consolidados para parsing de diferentes campos
- ✅ **TransactionService**: Testes para AddSingleTransactionAsync e AddTransactionToContextAsync
- ✅ **FileService**: Testes consolidados para validação de extensões e conteúdo
- ✅ **UnitOfWork**: Testes completos para gerenciamento de transações (com supressão de warnings do InMemory)

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

---

## Contact & Contributing

For questions, bug reports, or feature requests, please contact the development team.

---

*Last Updated: December 29, 2025*
