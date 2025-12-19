# CNAB Transactions - Backend Challenge

API e frontend para upload de arquivos CNAB, parsing, persistência em PostgreSQL e consulta de transações/saldo por CPF. Projeto empacotado com Docker Compose.

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
- Docker e Docker Compose
- .NET 9 SDK (para rodar localmente sem Docker)
- Node 20+ (apenas se quiser rodar o frontend fora do Docker)

## Como rodar com Docker (recomendado)
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
- backend.IntegrationTests/ — testes de integração
- frontend/ — app React (upload/consulta)
- API_DOCUMENTATION.md — referência completa da API

## Licença
Uso interno para o desafio técnico.
