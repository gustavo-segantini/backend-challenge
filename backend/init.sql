-- Create EF Migrations History table
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" VARCHAR(150) PRIMARY KEY,
    "ProductVersion" VARCHAR(32) NOT NULL
);

-- Create Stores table
CREATE TABLE IF NOT EXISTS "Stores" (
    "Id" SERIAL PRIMARY KEY,
    "CpfCnpj" VARCHAR(10) NOT NULL,
    "Card" VARCHAR(25) NOT NULL,
    "Balance" NUMERIC(15,2) NOT NULL,
    UNIQUE ("CpfCnpj", "Card")
);

-- Create Transactions table
CREATE TABLE IF NOT EXISTS "Transactions" (
    "Id" SERIAL PRIMARY KEY,
    "BankCode" VARCHAR(4) NOT NULL,
    "Account" VARCHAR(7) NOT NULL,
    "NatureCode" VARCHAR(5) NOT NULL,
    "Amount" NUMERIC(15,2) NOT NULL,
    "CpfCnpj" VARCHAR(10) NOT NULL,
    "Card" VARCHAR(25) NOT NULL,
    "TransactionDate" TIMESTAMP WITH TIME ZONE NOT NULL,
    "TransactionTime" INTERVAL NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL
);

-- Create index on Transactions
CREATE INDEX IF NOT EXISTS "IX_Transactions_TransactionDate_CpfCnpj" 
ON "Transactions" ("TransactionDate", "CpfCnpj");

-- Record the migration
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") 
VALUES ('20251218161537_InitialCreate', '9.0.0')
ON CONFLICT DO NOTHING;
