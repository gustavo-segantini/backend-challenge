# API Documentation - CNAB Transaction System

## Base URL
```
http://localhost:5000/api
```

## Overview
This API provides endpoints for managing CNAB (Centro Nacional de Automação Bancária) file uploads and querying transaction data. The system parses CNAB format files, stores transactions in a PostgreSQL database, and provides endpoints to query transaction history and calculate balances by CPF.

---

## Endpoints

### 1. Upload CNAB File
Upload and process a CNAB format file containing transaction records.

**Endpoint:** `POST /api/transactions/upload`

**Content-Type:** `multipart/form-data`

**Request:**
```bash
curl -X POST http://localhost:5000/api/transactions/upload \
  -F "file=@CNAB.txt"
```

**Request Body:**
- `file` (file, required): CNAB text file (.txt) with transaction records

**CNAB File Format:**
Each line represents one transaction with 80 characters in fixed positions:
- Position 0-1: Transaction Type (1 character)
- Position 1-9: Date (YYYYMMDD format)
- Position 9-19: Amount (10 digits, last 2 are decimals)
- Position 19-30: CPF (11 characters)
- Position 30-42: Card number (12 characters)
- Position 42-48: Time (HHMMSS format)
- Position 48-62: Store Owner name (14 characters)
- Position 62-80: Store Name (18 characters)

**Transaction Types:**
- `1` - Debit (Income)
- `2` - Boleto (Expense)
- `3` - Financing (Expense)
- `4` - Credit (Income)
- `5` - Loan Receipt (Income)
- `6` - Sales (Income)
- `7` - TED Receipt (Income)
- `8` - DOC Receipt (Income)
- `9` - Rent (Expense)

**Success Response (200 OK):**
```json
{
  "message": "Successfully imported 46 transactions",
  "count": 46
}
```

**Error Response (400 Bad Request):**
```json
{
  "error": "Conteúdo do arquivo está vazio."
}
```

**Error Scenarios:**
- Empty file
- Invalid file format
- Line length < 80 characters
- Invalid date/time format
- Invalid amount format
- No valid transactions found

---

### 2. Get Transactions by CPF
Retrieve all transactions for a specific CPF, ordered by date (most recent first).

**Endpoint:** `GET /api/transactions/{cpf}`

**Request:**
```bash
curl -X GET http://localhost:5000/api/transactions/09620676017
```

**Path Parameters:**
- `cpf` (string, required): 11-digit CPF number

**Success Response (200 OK):**
```json
[
  {
    "id": 1,
    "bankCode": "1",
    "cpf": "09620676017",
    "natureCode": "1",
    "amount": 150.00,
    "card": "1234****7890",
    "storeOwner": "JOÃO MACEDO",
    "storeName": "BAR DO JOÃO",
    "transactionDate": "2019-03-01T00:00:00Z",
    "transactionTime": "23:30:00",
    "createdAt": "2025-12-19T16:30:00Z",
    "transactionDescription": "Debit",
    "signedAmount": 150.00
  },
  {
    "id": 2,
    "bankCode": "2",
    "cpf": "09620676017",
    "natureCode": "2",
    "amount": 142.00,
    "card": "3153****3453",
    "storeOwner": "JOÃO MACEDO",
    "storeName": "BAR DO JOÃO",
    "transactionDate": "2019-03-01T00:00:00Z",
    "transactionTime": "15:34:53",
    "createdAt": "2025-12-19T16:30:00Z",
    "transactionDescription": "Boleto",
    "signedAmount": -142.00
  }
]
```

**Empty Result (200 OK):**
```json
[]
```

**Error Response (400 Bad Request):**
```json
{
  "error": "CPF inválido"
}
```

**Response Fields:**
- `id`: Unique transaction identifier
- `bankCode`: Bank/transaction code (1-9)
- `cpf`: Customer CPF (11 digits)
- `natureCode`: Transaction nature code (determines income/expense)
- `amount`: Transaction amount (absolute value)
- `card`: Card number (partially masked)
- `storeOwner`: Name of the store owner
- `storeName`: Name of the store
- `transactionDate`: Date when transaction occurred (ISO 8601)
- `transactionTime`: Time when transaction occurred (HH:mm:ss)
- `createdAt`: Timestamp when record was created in system (ISO 8601)
- `transactionDescription`: Human-readable transaction type
- `signedAmount`: Amount with sign (positive for income, negative for expense)

---

### 3. Get Balance by CPF
Calculate and return the total balance for a specific CPF (sum of all signed amounts).

**Endpoint:** `GET /api/transactions/{cpf}/balance`

**Request:**
```bash
curl -X GET http://localhost:5000/api/transactions/09620676017/balance
```

**Path Parameters:**
- `cpf` (string, required): 11-digit CPF number

**Success Response (200 OK):**
```json
{
  "balance": 8.00
}
```

**Balance Calculation:**
- Income transactions (types 1, 4, 5, 6, 7, 8): **+** amount
- Expense transactions (types 2, 3, 9): **-** amount
- Final balance = sum of all signed amounts

**Example:**
```
Debit (type 1):      +150.00
Boleto (type 2):     -142.00
Credit (type 4):      +50.00
Financing (type 3):   -30.00
-------------------------
Balance:              +28.00
```

**Error Response (400 Bad Request):**
```json
{
  "error": "CPF inválido"
}
```

---

### 4. Clear All Data
Delete all transactions from the database. **This operation cannot be undone.**

**Endpoint:** `DELETE /api/transactions`

**Request:**
```bash
curl -X DELETE http://localhost:5000/api/transactions
```

**Success Response (200 OK):**
```json
{
  "message": "All data cleared successfully"
}
```

**Error Response (400 Bad Request):**
```json
{
  "error": "Falha ao limpar dados"
}
```

**Warning:** This endpoint deletes **ALL** transaction records permanently. Use with caution.

---

## HTTP Status Codes

| Status Code | Description |
|------------|-------------|
| 200 OK | Request successful |
| 400 Bad Request | Invalid input or business logic error |
| 500 Internal Server Error | Unexpected server error |

---

## Error Response Format

All error responses follow this structure:
```json
{
  "error": "Description of the error in Portuguese"
}
```

---

## Common Use Cases

### 1. Import and Query Workflow
```bash
# Step 1: Upload CNAB file
curl -X POST http://localhost:5000/api/transactions/upload \
  -F "file=@CNAB.txt"

# Response: { "message": "Successfully imported 46 transactions", "count": 46 }

# Step 2: Query transactions for a specific CPF
curl -X GET http://localhost:5000/api/transactions/09620676017

# Step 3: Check balance
curl -X GET http://localhost:5000/api/transactions/09620676017/balance

# Response: { "balance": 8.00 }
```

### 2. Clear and Re-import
```bash
# Step 1: Clear existing data
curl -X DELETE http://localhost:5000/api/transactions

# Step 2: Import new file
curl -X POST http://localhost:5000/api/transactions/upload \
  -F "file=@new_CNAB.txt"
```

---

## Testing with Postman

### 1. Import Collection
Create a new Postman collection with these endpoints:

**Collection Name:** CNAB Transaction API

**Environment Variables:**
```
base_url = http://localhost:5000/api
test_cpf = 09620676017
```

### 2. Upload File Request
- Method: POST
- URL: `{{base_url}}/transactions/upload`
- Body: form-data
  - Key: `file`
  - Type: File
  - Value: Select CNAB.txt file

### 3. Get Transactions Request
- Method: GET
- URL: `{{base_url}}/transactions/{{test_cpf}}`

### 4. Get Balance Request
- Method: GET
- URL: `{{base_url}}/transactions/{{test_cpf}}/balance`

### 5. Clear Data Request
- Method: DELETE
- URL: `{{base_url}}/transactions`

---

## Frontend Integration

### Example with Axios (React)

```javascript
import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

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

## Versioning

**Current Version:** 1.0.0

**Changelog:**
- v1.0.0 (2025-12-19): Initial release
  - CNAB file upload
  - Transaction query by CPF
  - Balance calculation
  - Data clearing functionality
  - Store information support

---

## Contact & Contributing

For questions, bug reports, or feature requests, please contact the development team.

---

*Last Updated: December 19, 2025*
