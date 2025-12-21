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
