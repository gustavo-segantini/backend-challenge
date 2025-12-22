# 🏦 CNAB Parser API - Backend Challenge

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com)
[![Tests](https://img.shields.io/badge/tests-268%20passing-brightgreen)](https://github.com)
[![Coverage](https://img.shields.io/badge/coverage-86.7%25-brightgreen)](https://github.com)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

A robust, production-ready API for processing and analyzing CNAB files with JWT authentication, GitHub OAuth, and enterprise-grade features like structured logging, robust validation, and comprehensive tests.

## 📋 Table of Contents

- [Overview](#overview)
- [Technologies](#technologies)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Detailed Configuration](#detailed-configuration)
- [API Usage](#api-usage)
- [Development](#development)
- [Tests](#tests)
- [Troubleshooting](#troubleshooting)
- [Documentation](#documentation)

## 🎯 Overview

**CNAB Parser API** is a complete solution for processing CNAB files (National Standardized Configuration for Banking Applications), providing:

✅ **CNAB file upload and parsing** with rigorous validation  
✅ **Versioned RESTful API** (`/api/v1/`) with JWT + GitHub OAuth authentication  
✅ **Pagination, filtering, and sorting** on transaction queries  
✅ **Structured logging** with end-to-end correlation ID (Serilog)  
✅ **Robust validation** with FluentValidation (real CPF, credentials)  
✅ **Comprehensive tests** (268 tests with 86.7% coverage)  
✅ **Docker Compose** for development and production  
✅ **Application Insights** ready for production telemetry  
✅ **ProblemDetails RFC 7807** for standardized HTTP responses  
✅ **Swagger/OpenAPI** with interactive documentation  

## 🛠️ Technologies

| Layer | Technology | Version | Purpose |
|--------|-----------|--------|----------|
| **Runtime** | .NET | 9.0/10.0 | Execution |
| **Web Framework** | ASP.NET Core | Latest | HTTP APIs |
| **Database** | PostgreSQL | 15 | Persistence |
| **ORM** | Entity Framework Core | Latest | Data Access |
| **Logging** | Serilog | 4.2.0 | Structured Logs |
| **Validation** | FluentValidation | 11.11.0 | Input Validation |
| **Errors** | ProblemDetails Middleware | 6.4.1 | RFC 7807 |
| **API Version** | Microsoft.AspNetCore.Mvc.Versioning | 5.1.0 | v1, v2... |
| **Testing** | xUnit + Moq | Latest | Tests |
| **Frontend** | React | 19 | UI |
| **Containers** | Docker | Latest | Orchestration |

## Architecture
- REST API: [backend/Program.cs](backend/Program.cs) with controllers in [backend/Controllers](backend/Controllers).
- Domain/services layer: parser, upload, transactions, and files in [backend/Services](backend/Services).
- Persistence: EF Core + migrations in [backend/Data](backend/Data).
- Middleware: global error handling (ExceptionHandlingMiddleware).

## Prerequisites

**Minimum (recommended):**
- Docker Desktop ([Download](https://www.docker.com/products/docker-desktop))

**Optional (local development):**
- .NET 9 SDK
- Node 20+
- PostgreSQL 16

## Running with Docker (recommended)

### Option 1 - Automated Setup (recommended)

```bash
# Windows
setup.bat

# macOS / Linux / WSL
bash setup.sh
```

The script automatically:
1. ✅ Checks if Docker is installed and running
2. ✅ Creates `.env` file (if it doesn't exist)
3. ✅ Builds containers
4. ✅ Brings up all services
5. ✅ Waits for them to be healthy (30s)

### Option 2 - Manual Command

```bash
docker-compose up --build
```

### Available Services

| Service | URL | Description |
|---------|-----|-----------|
| **Frontend** | http://localhost:3000 | CNAB upload interface |
| **API** | http://localhost:5000 | Backend REST API |
| **Swagger** | http://localhost:5000/swagger | Interactive documentation |
| **Database** | localhost:5432 | PostgreSQL (postgres/postgres) |
| **Health Check** | http://localhost:5000/api/v1/health | Application status |
| **Prometheus Metrics** | http://localhost:5000/metrics | Metrics for Prometheus/Grafana |

### Application Health and Monitoring

```bash
# Simple health check (returns "Healthy")
curl http://localhost:5000/api/v1/health

# Prometheus metrics (for scraping)
curl http://localhost:5000/metrics

# Readiness probe (k8s)
curl http://localhost:5000/api/v1/health/ready

# Liveness probe (k8s)
curl http://localhost:5000/api/v1/health/live
```

### Useful Commands

```bash
# Check service status
docker-compose ps

# View logs in real-time
docker-compose logs -f api              # API logs
docker-compose logs -f frontend         # Frontend logs
docker-compose logs -f                  # All logs

# Stop services
docker-compose down

# Restart everything
docker-compose down && docker-compose up -d --build

# Clean volumes (recreates database)
docker-compose down -v
```

## Running Only the API (without Docker)

### Backend

Prerequisites: .NET 9 SDK + PostgreSQL 16

```bash
# 1. Install dependencies
cd backend
dotnet restore

# 2. Configure database (optional)
$env:ConnectionStrings__PostgresConnection = "Host=localhost;Port=5432;Database=cnab_db;Username=postgres;Password=postgres"

# 3. Apply migrations
dotnet ef database update

# 4. Run API
dotnet run
```

API runs at: http://localhost:5000

### Frontend

Prerequisites: Node.js 20+

```bash
cd frontend
npm install
npm start
```

Frontend runs at: http://localhost:3000

## Tests

### Running Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test backend.Tests/CnabApi.Tests.csproj

# Integration tests only
dotnet test backend.IntegrationTests/CnabApi.IntegrationTests.csproj
```

### Code Coverage

The project has **86.7% line coverage**, **77.27% branch coverage**, and **90.5% method coverage** (268 tests).

#### Generate Coverage Report

```bash
# 1. Run tests with coverage (generates coverage.cobertura.xml)
dotnet test backend.Tests/CnabApi.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# 2. Generate HTML report (requires reportgenerator)
reportgenerator -reports:backend.Tests/coverage.cobertura.xml -targetdir:backend.Tests/TestResults/CoverageReport -reporttypes:Html

# 3. Open report in browser
# Windows
start backend.Tests/TestResults/CoverageReport/index.html
# macOS
open backend.Tests/TestResults/CoverageReport/index.html
# Linux
xdg-open backend.Tests/TestResults/CoverageReport/index.html
```

#### Install ReportGenerator (first time)

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

#### What's Excluded from Coverage

Infrastructure code marked with `[ExcludeFromCodeCoverage]`:
- ✅ EF Core migrations
- ✅ Program.cs (startup configuration)
- ✅ Configuration extensions (ServiceCollection, Middleware, HealthChecks)
- ✅ DataSeeder
- ✅ Exception handling middleware

This ensures coverage reflects only **testable business code**.

## Main Endpoints

- `POST /api/transactions/upload` — upload CNAB file
- `GET /api/transactions/{cpf}` — list transactions by CPF
- `GET /api/transactions/{cpf}/balance` — CPF balance
- `DELETE /api/transactions` — clear data

Details: [API_DOCUMENTATION.md](API_DOCUMENTATION.md)

## Environment Variables

The `.env` file controls configuration:

```bash
POSTGRES_USER=postgres              # Database user
POSTGRES_PASSWORD=postgres          # Database password
API_PORT=5000                       # API port
FRONTEND_PORT=3000                  # Frontend port
ASPNETCORE_ENVIRONMENT=Production   # Mode (Production/Development)
```

To customize, edit `.env` and restart:

```bash
docker-compose down
docker-compose up -d --build
```

## Troubleshooting

### "Docker is not installed"
- Install [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Restart your computer
- Run setup again

### "Docker daemon is not running"
- Open Docker Desktop
- Wait until it's ready
- Run setup again

### "Port 5000 is already in use"
```bash
API_PORT=5001              # Edit .env
docker-compose down && docker-compose up -d --build
```

### "Frontend cannot connect to API"
```bash
docker-compose logs api    # Check logs
```
- Clear browser cache (Ctrl+Shift+Delete)
- Check if API is running at http://localhost:5000/swagger

### "Database won't start"
```bash
docker-compose down -v     # Remove volumes
docker-compose up -d --build
```

### View detailed logs
```bash
docker-compose logs postgres              # Full log
docker-compose logs postgres --tail=50    # Last 50 lines
```

## Helpful Tips

- **First run**: may take 5-10 minutes for downloads and build
- **Before git pull**: always run `docker-compose down`
- **For troubleshooting**: use `docker-compose logs -f` to see logs in real-time
- **Containers restart automatically** (`restart: unless-stopped`)

## Project Structure

```
backend-challenge/
├── backend/                    # ASP.NET Core 9 API
│   ├── Controllers/            # REST endpoints
│   ├── Services/               # Business logic
│   ├── Models/                 # DTOs and entities
│   ├── Data/                   # EF Core + migrations
│   └── Dockerfile              # Production build
│
├── backend.Tests/              # Unit tests (xUnit)
│   ├── Services/               # Service tests
│   ├── Controllers/            # Controller tests
│   └── Utilities/              # Utility tests
│
├── backend.IntegrationTests/   # Integration tests
│
├── frontend/                   # React app
│   ├── public/                 # Static HTML
│   ├── src/                    # Components
│   └── Dockerfile              # Production build
│
├── docker-compose.yml          # Orchestration
├── .env.example                # Variables template
├── setup.bat                   # Windows setup
├── setup.sh                    # Unix setup
│
├── README.md                   # This file
├── API_DOCUMENTATION.md        # Endpoint reference
├── ROADMAP.md                  # Development plan
└── SETUP_VERIFICATION.md       # Verification checklist
```

**Total tests**: 268 (xUnit + Moq)  
**Coverage**: 86.7% line, 77.27% branch, 90.5% method

## 📚 Documentation

- [API_DOCUMENTATION.md](API_DOCUMENTATION.md) - Complete API reference with curl/Postman examples
- [ROADMAP.md](ROADMAP.md) - Development plan (upcoming features and timeline)

## 🏗️ Architecture

- **Backend**: ASP.NET Core 9 + EF Core 9 + PostgreSQL 16
- **Frontend**: React 19 + Axios
- **Database**: PostgreSQL with automatic migrations
- **Cache**: Redis for performance
- **Testing**: xUnit + Moq + WebApplicationFactory
- **Deploy**: Docker Compose with health checks

## License

Internal use for the technical challenge.
