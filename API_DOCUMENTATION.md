# 📡 API Documentation - CNAB Parser

**Base URL**: `http://localhost:5000/api/v1`  
**Swagger**: `http://localhost:5000/swagger`  
**Version**: v1.0  
**Last Updated**: December 2025

## Índice

1. [Autenticação](#autenticação)
2. [Transações](#transações)
3. [Códigos de Status](#códigos-de-status)
4. [Modelos de Dados](#modelos-de-dados)
5. [Exemplos por Caso de Uso](#exemplos-por-caso-de-uso)

---

## Autenticação

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

## Transações

### POST /transactions/upload

Fazer upload e processar arquivo CNAB.

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

**Response** (200 OK):
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

**Exemplo cURL**:
```bash
curl -X POST http://localhost:5000/api/v1/transactions/upload \
  -H "Authorization: Bearer {accessToken}" \
  -F "file=@cnab.txt"
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

**Última atualização**: Dezembro 2025  
**Versão**: v1.0

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
| **Line Coverage** | 86.7% | ✅ Excelente |
| **Branch Coverage** | 77.27% | ✅ Muito Bom |
| **Method Coverage** | 90.5% | ✅ Excelente |
| **Total de Testes** | 268 | - |

### Executar Testes

```bash
# Todos os testes
dotnet test

# Apenas unitários
dotnet test backend.Tests/CnabApi.Tests.csproj

# Apenas integração
dotnet test backend.IntegrationTests/CnabApi.IntegrationTests.csproj
```

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

Isso garante que as métricas refletem apenas **código de negócio testável**.

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
  - 86.7% test coverage (268 tests)

---

## Contact & Contributing

For questions, bug reports, or feature requests, please contact the development team.

---

*Last Updated: December 21, 2025*
