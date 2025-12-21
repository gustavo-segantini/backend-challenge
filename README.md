# 🏦 CNAB Parser API - Backend Challenge

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com)
[![Tests](https://img.shields.io/badge/tests-175%20passing-brightgreen)](https://github.com)
[![Coverage](https://img.shields.io/badge/coverage-%3E80%25-green)](https://github.com)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

Uma API robusta, production-ready para processamento e análise de arquivos CNAB com autenticação JWT, OAuth GitHub, e recursos enterprise como logging estruturado, validação robusta e testes abrangentes.

## 📋 Índice

- [Visão Geral](#visão-geral)
- [Tecnologias](#tecnologias)
- [Pré-requisitos](#pré-requisitos)
- [Setup Rápido](#setup-rápido)
- [Configuração Detalhada](#configuração-detalhada)
- [Uso da API](#uso-da-api)
- [Desenvolvimento](#desenvolvimento)
- [Testes](#testes)
- [Troubleshooting](#troubleshooting)
- [Documentação](#documentação)

## 🎯 Visão Geral

**CNAB Parser API** é uma solução completa para processar arquivos CNAB (Configuração Nacional Aplicativo Computadorial Bancário), fornecendo:

✅ **Upload e parser de arquivos CNAB** com validação rigorosa  
✅ **API RESTful versioned** (`/api/v1/`) com autenticação JWT + OAuth GitHub  
✅ **Paginação, filtros e ordenação** em consultas de transações  
✅ **Logging estruturado** com correlation ID end-to-end (Serilog)  
✅ **Validação robusta** com FluentValidation (CPF real, credenciais)  
✅ **Testes abrangentes** (175 testes: unitários + integração)  
✅ **Docker Compose** para desenvolvimento e produção  
✅ **Application Insights** pronto para telemetria em produção  
✅ **ProblemDetails RFC 7807** para respostas HTTP padronizadas  
✅ **Swagger/OpenAPI** com documentação interativa  

## 🛠️ Tecnologias

| Camada | Tecnologia | Versão | Propósito |
|--------|-----------|--------|----------|
| **Runtime** | .NET | 9.0/10.0 | Execução |
| **Web Framework** | ASP.NET Core | Latest | APIs HTTP |
| **Database** | PostgreSQL | 15 | Persistência |
| **ORM** | Entity Framework Core | Latest | Acesso a dados |
| **Logging** | Serilog | 4.2.0 | Logs estruturados |
| **Validação** | FluentValidation | 11.11.0 | Validação de inputs |
| **Errors** | ProblemDetails Middleware | 6.4.1 | RFC 7807 |
| **API Version** | Microsoft.AspNetCore.Mvc.Versioning | 5.1.0 | v1, v2... |
| **Testing** | xUnit + Moq | Latest | Testes |
| **Frontend** | React | 19 | UI |
| **Containers** | Docker | Latest | Orquestração |

## Arquitetura
- API REST: [backend/Program.cs](backend/Program.cs) com controllers em [backend/Controllers](backend/Controllers).
- Camada de domínio/serviços: parser, upload, transações e arquivos em [backend/Services](backend/Services).
- Persistência: EF Core + migrations em [backend/Data](backend/Data).
- Middleware: tratamento global de erros (ExceptionHandlingMiddleware).

## Pré-requisitos

**Mínimo (recomendado):**
- Docker Desktop ([Download](https://www.docker.com/products/docker-desktop))

**Opcional (desenvolvimento local):**
- .NET 9 SDK
- Node 20+
- PostgreSQL 16

## Como rodar com Docker (recomendado)

### Opção 1 - Setup automático (recomendado)

```bash
# Windows
setup.bat

# macOS / Linux / WSL
bash setup.sh
```

O script automaticamente:
1. ✅ Verifica se Docker está instalado e rodando
2. ✅ Cria arquivo `.env` (caso não exista)
3. ✅ Faz build dos containers
4. ✅ Sobe todos os serviços
5. ✅ Aguarda até ficarem healthy (30s)

### Opção 2 - Comando manual

```bash
docker-compose up --build
```

### Serviços Disponíveis

| Serviço | URL | Descrição |
|---------|-----|-----------|
| **Frontend** | http://localhost:3000 | Interface de upload de CNAB |
| **API** | http://localhost:5000 | Backend REST API |
| **Swagger** | http://localhost:5000/swagger | Documentação interativa |
| **Database** | localhost:5432 | PostgreSQL (postgres/postgres) |
| **Health Check** | http://localhost:5000/api/v1/health | Status da aplicação |
| **Prometheus Metrics** | http://localhost:5000/metrics | Métricas para Prometheus/Grafana |

### Monitoramento e Saúde da Aplicação

```bash
# Health check simples (retorna "Healthy")
curl http://localhost:5000/api/v1/health

# Métricas Prometheus (para scraping)
curl http://localhost:5000/metrics

# Readiness probe (k8s)
curl http://localhost:5000/api/v1/health/ready

# Liveness probe (k8s)
curl http://localhost:5000/api/v1/health/live
```

### Comandos Úteis

```bash
# Ver status dos serviços
docker-compose ps

# Ver logs em tempo real
docker-compose logs -f api              # Logs da API
docker-compose logs -f frontend         # Logs do Frontend
docker-compose logs -f                  # Todos os logs

# Parar serviços
docker-compose down

# Reiniciar tudo
docker-compose down && docker-compose up -d --build

# Limpar volumes (recria banco)
docker-compose down -v
```

## Como rodar só a API (sem Docker)

### Backend

Pré-requisitos: .NET 9 SDK + PostgreSQL 16

```bash
# 1. Instalar dependências
cd backend
dotnet restore

# 2. Configurar banco (opcional)
$env:ConnectionStrings__PostgresConnection = "Host=localhost;Port=5432;Database=cnab_db;Username=postgres;Password=postgres"

# 3. Aplicar migrations
dotnet ef database update

# 4. Rodar API
dotnet run
```

API fica em: http://localhost:5000

### Frontend

Pré-requisitos: Node.js 20+

```bash
cd frontend
npm install
npm start
```

Frontend fica em: http://localhost:3000

## Testes

```bash
# Todos os testes
dotnet test

# Apenas unitários
dotnet test backend.Tests/CnabApi.Tests.csproj

# Apenas integração
dotnet test backend.IntegrationTests/CnabApi.IntegrationTests.csproj

# Com coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

## Endpoints Principais

- `POST /api/transactions/upload` — upload de arquivo CNAB
- `GET /api/transactions/{cpf}` — lista transações do CPF
- `GET /api/transactions/{cpf}/balance` — saldo do CPF
- `DELETE /api/transactions` — limpa dados

Detalhes: [API_DOCUMENTATION.md](API_DOCUMENTATION.md)

## Variáveis de Ambiente

Arquivo `.env` controla a configuração:

```bash
POSTGRES_USER=postgres              # Usuário do banco
POSTGRES_PASSWORD=postgres          # Senha do banco
API_PORT=5000                       # Porta da API
FRONTEND_PORT=3000                  # Porta do frontend
ASPNETCORE_ENVIRONMENT=Production   # Modo (Production/Development)
```

Para customizar, edite `.env` e reinicie:

```bash
docker-compose down
docker-compose up -d --build
```

## Troubleshooting

### "Docker is not installed"
- Instale [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Reinicie o computador
- Execute setup novamente

### "Docker daemon is not running"
- Abra Docker Desktop
- Aguarde até que esteja pronto
- Execute setup novamente

### "Port 5000 is already in use"
```bash
API_PORT=5001              # Edite .env
docker-compose down && docker-compose up -d --build
```

### "Frontend não conecta com API"
```bash
docker-compose logs api    # Verifique logs
```
- Limpe cache do navegador (Ctrl+Shift+Delete)
- Verifique se API está em http://localhost:5000/swagger

### "Banco de dados não sobe"
```bash
docker-compose down -v     # Remove volumes
docker-compose up -d --build
```

### Ver logs detalhados
```bash
docker-compose logs postgres              # Log completo
docker-compose logs postgres --tail=50    # Últimas 50 linhas
```

## Dicas Úteis

- **Primeira execução**: pode levar 5-10 minutos para downloads e build
- **Antes de git pull**: sempre execute `docker-compose down`
- **Para troubleshooting**: use `docker-compose logs -f` para ver logs em tempo real
- **Containers reiniçiam automaticamente** (`restart: unless-stopped`)

## Estrutura do Projeto

```
backend-challenge/
├── backend/                    # API ASP.NET Core 9
│   ├── Controllers/            # Endpoints REST
│   ├── Services/               # Lógica de negócio
│   ├── Models/                 # DTOs e entidades
│   ├── Data/                   # EF Core + migrations
│   └── Dockerfile              # Build produção
│
├── backend.Tests/              # Testes unitários (xUnit)
│   ├── Services/               # Testes de serviços
│   ├── Controllers/            # Testes de controllers
│   └── Utilities/              # Testes de utilitários
│
├── backend.IntegrationTests/   # Testes de integração
│
├── frontend/                   # React app
│   ├── public/                 # HTML estático
│   ├── src/                    # Componentes
│   └── Dockerfile              # Build produção
│
├── docker-compose.yml          # Orquestração
├── .env.example                # Template de variáveis
├── setup.bat                   # Setup Windows
├── setup.sh                    # Setup Unix
│
├── README.md                   # Este arquivo
├── GETTING_STARTED.md          # Guia detalhado
├── API_DOCUMENTATION.md        # Referência de endpoints
├── ROADMAP.md                  # Plano de desenvolvimento
└── SETUP_VERIFICATION.md       # Checklist de verificação
```

**Total de testes**: 175 (xUnit + Moq)  
**Cobertura**: CursorPaginationHelper (18), AuthService (23), + testes existentes

## 📚 Documentação

- [GETTING_STARTED.md](GETTING_STARTED.md) - Guia detalhado com mais exemplos e troubleshooting avançado
- [API_DOCUMENTATION.md](API_DOCUMENTATION.md) - Referência completa de endpoints com exemplos curl/Postman
- [ROADMAP.md](ROADMAP.md) - Plano de desenvolvimento (próximas features e timeline)

## 🏗️ Arquitetura

- **Backend**: ASP.NET Core 9 + EF Core 9 + PostgreSQL 16
- **Frontend**: React 18 + Axios
- **Database**: PostgreSQL com migrations automáticas
- **Cache**: Redis para performance
- **Testes**: xUnit + Moq + WebApplicationFactory
- **Deploy**: Docker Compose com health checks

## Licença

Uso interno para o desafio técnico.
