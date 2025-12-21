# CNAB Transactions - Backend Challenge

API e frontend para upload de arquivos CNAB, parsing, persistência em PostgreSQL e consulta de transações/saldo por CPF. Projeto empacotado com Docker Compose.

## 🚀 Início Rápido

**One-command setup** (Windows, macOS, Linux):

### Windows
```bash
setup.bat
```

### macOS/Linux/WSL
```bash
bash setup.sh
```

**Depois acesse:** http://localhost:3000

👉 Para instruções detalhadas, veja [GETTING_STARTED.md](GETTING_STARTED.md)

---

## Visão Geral
- Backend: ASP.NET Core 9 (C#), EF Core, PostgreSQL, Swagger/OpenAPI.
- Frontend: React com formulário de upload e consultas.
- Testes: xUnit + FluentAssertions; testes de integração usando WebApplicationFactory.
- Deploy local: Docker Compose com `api`, `postgres`, `frontend`.

Documentação completa dos endpoints: [API_DOCUMENTATION.md](API_DOCUMENTATION.md).

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

**Opção 1 - Setup automático (recomendado):**

```bash
# Windows
setup.bat

# macOS / Linux / WSL
bash setup.sh
```

**Opção 2 - Comando manual:**

Na raiz do repositório:
```bash
docker-compose up --build
```

Serviços após subir:
- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger
- Frontend: http://localhost:3000
- PostgreSQL: localhost:5432 (user: postgres, password: postgres)

Para derrubar: `docker-compose down`.

## Como rodar só a API (sem Docker)
1) Configurar connection string (opcional) via variável `ConnectionStrings__PostgresConnection` ou editar `appsettings.json`.
2) Rodar migrations (opcional em ambiente de dev usando InMemory):

```bash
dotnet ef database update --project backend
```

3) Executar API:

```bash
dotnet run --project backend
```

API ficará em http://localhost:5000 (Swagger em /swagger).

## Testes
- Testes unitários e de integração:

```bash
dotnet test
```

- Coverage (exemplo):

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Endpoints principais
- POST /api/transactions/upload — upload de arquivo CNAB (multipart/form-data)
- GET /api/transactions/{cpf} — lista transações do CPF
- GET /api/transactions/{cpf}/balance — saldo consolidado do CPF
- DELETE /api/transactions — limpa dados

Detalhes, exemplos de curl/Postman e formatos estão em [API_DOCUMENTATION.md](API_DOCUMENTATION.md).

## Variáveis de ambiente úteis
- `ConnectionStrings__PostgresConnection`: connection string do PostgreSQL
- `ASPNETCORE_ENVIRONMENT`: `Development`, `Production` ou `Test`

## Troubleshooting rápido
- Porta 5000 ocupada: ajuste `ASPNETCORE_URLS` ou mapeamento no docker-compose.
- Banco não sobe: verifique se a porta 5432 está livre; use `docker-compose logs postgres`.
- Swagger não carrega: confirme que a API está em execução e acessando `/swagger`.
- Testes de integração: garantem uso de banco InMemory quando `ASPNETCORE_ENVIRONMENT=Test`.

## Estrutura de pastas (essencial)
- backend/ — API ASP.NET Core + EF Core
- backend.Tests/ — testes unitários
- backend.IntegrationTests/ — testes de integração
- frontend/ — app React (upload/consulta)
- API_DOCUMENTATION.md — referência completa da API
- GETTING_STARTED.md — guia passo-a-passo (recomendado ler primeiro!)

## 📚 Documentação

- [GETTING_STARTED.md](GETTING_STARTED.md) - **Comece aqui!** Instruções de setup e troubleshooting
- [API_DOCUMENTATION.md](API_DOCUMENTATION.md) - Referência de endpoints da API
- [ROADMAP.md](ROADMAP.md) - Plano de desenvolvimento e próximos passos

## Licença
Uso interno para o desafio técnico.
