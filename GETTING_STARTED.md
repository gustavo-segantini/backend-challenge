# 🚀 Getting Started - CNAB Transaction Manager

Instruções para rodar o projeto em qualquer computador (Windows, macOS ou Linux).

---

## ✅ Pré-requisitos

Você precisa ter apenas **Docker Desktop** instalado:

- **Windows/macOS**: [Docker Desktop](https://www.docker.com/products/docker-desktop)
- **Linux**: [Docker Engine](https://docs.docker.com/engine/install/) + [Docker Compose](https://docs.docker.com/compose/install/)

**Nenhuma outra ferramenta é necessária!**

---

## 🎯 Início Rápido (One Command)

### Windows
```bash
setup.bat
```

### macOS / Linux / WSL
```bash
bash setup.sh
```

**Pronto!** O script vai:
1. ✅ Verificar se Docker está instalado e rodando
2. ✅ Copiar arquivo `.env` automaticamente
3. ✅ Fazer build dos containers
4. ✅ Subir todos os serviços
5. ✅ Aguardar os serviços ficarem prontos

---

## 📍 Acessar os Serviços

Após o setup completar, acesse:

| Serviço | URL | Descrição |
|---------|-----|-----------|
| **Frontend** | http://localhost:3000 | Interface de upload de CNAB |
| **API** | http://localhost:5000 | Backend REST API |
| **Swagger** | http://localhost:5000/swagger | Documentação interativa da API |
| **Database** | localhost:5432 | PostgreSQL (user: postgres, password: postgres) |

---

## 📋 Comandos Úteis

### Ver status dos serviços
```bash
docker-compose ps
```

### Ver logs em tempo real
```bash
# Logs da API
docker-compose logs -f api

# Logs do Frontend
docker-compose logs -f frontend

# Logs do Banco
docker-compose logs -f postgres

# Todos os logs
docker-compose logs -f
```

### Parar os serviços
```bash
docker-compose down
```

### Reiniciar tudo
```bash
docker-compose down && docker-compose up -d --build
```

### Limpar tudo (remover volumes)
```bash
docker-compose down -v
```

---

## 🛠️ Desenvolvimento Local (sem Docker)

Se preferir rodar sem Docker:

### Backend (API)
```bash
# Pré-requisitos:
# - .NET 9 SDK
# - PostgreSQL 16

# 1. Instalar dependências
cd backend
dotnet restore

# 2. Configurar banco de dados (variável de ambiente)
$env:ConnectionStrings__PostgresConnection = "Host=localhost;Port=5432;Database=cnab_db;Username=postgres;Password=postgres"

# 3. Aplicar migrations
dotnet ef database update

# 4. Rodar API
dotnet run
```

API ficará em: http://localhost:5000

### Frontend (React)
```bash
# Pré-requisitos:
# - Node.js 20+

# 1. Instalar dependências
cd frontend
npm install

# 2. Rodar frontend em desenvolvimento
npm start
```

Frontend ficará em: http://localhost:3000

---

## 🧪 Testes

### Todos os testes
```bash
dotnet test
```

### Apenas testes unitários
```bash
dotnet test backend.Tests/CnabApi.Tests.csproj
```

### Apenas testes de integração
```bash
dotnet test backend.IntegrationTests/CnabApi.IntegrationTests.csproj
```

### Com coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## 📁 Estrutura do Projeto

```
backend-challenge/
├── backend/                    # API ASP.NET Core
│   ├── Controllers/            # Endpoints REST
│   ├── Services/               # Lógica de negócio
│   ├── Models/                 # DTOs e entidades
│   ├── Data/                   # EF Core + migrations
│   └── Dockerfile              # Build da API
│
├── backend.Tests/              # Testes unitários
│   ├── Services/               # Testes de serviços
│   ├── Controllers/            # Testes de controllers
│   └── Utilities/              # Testes de utilitários
│
├── backend.IntegrationTests/   # Testes de integração
│   └── TransactionsControllerIntegrationTests.cs
│
├── frontend/                   # React app
│   ├── public/                 # HTML estático
│   ├── src/                    # Componentes React
│   └── Dockerfile              # Build do frontend
│
├── docker-compose.yml          # Orquestração dos containers
├── .env.example                # Template de variáveis
├── setup.bat                   # Script de setup (Windows)
├── setup.sh                    # Script de setup (Unix)
└── README.md                   # Documentação principal
```

---

## 🔧 Variáveis de Ambiente

As variáveis estão no arquivo `.env`. Se precisar customizar, edite e reinicie:

```bash
# Exemplo de customização
API_PORT=5001              # Mudar porta da API
FRONTEND_PORT=3001         # Mudar porta do frontend
ASPNETCORE_ENVIRONMENT=Development  # Modo desenvolvimento
```

Depois reinicie:
```bash
docker-compose down
docker-compose up -d --build
```

---

## ❌ Troubleshooting

### "Docker is not installed"
- Instale [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Reinicie o computador
- Execute o setup novamente

### "Docker daemon is not running"
- Abra Docker Desktop
- Aguarde até que o ícone não tenha mais aviso
- Execute o setup novamente

### "Port 5000 is already in use"
```bash
# Mude a porta no .env
API_PORT=5001
docker-compose down && docker-compose up -d --build
```

### "Frontend não conecta com API"
- Verifique se API está rodando: `docker-compose logs api`
- Verifique se frontend tem acesso correto: `http://localhost:5000/swagger`
- Limpe cache do navegador (Ctrl+Shift+Delete)

### "Banco de dados não sobe"
```bash
# Limpe tudo e tente novamente
docker-compose down -v
docker-compose up -d --build
```

### Ver logs detalhados
```bash
# Log completo de um serviço
docker-compose logs postgres

# Últimas 50 linhas
docker-compose logs postgres --tail=50
```

---

## 📚 Documentação Adicional

- [API_DOCUMENTATION.md](API_DOCUMENTATION.md) - Referência completa da API
- [README.md](README.md) - Documentação técnica do projeto
- [ROADMAP.md](ROADMAP.md) - Plano de desenvolvimento

---

## 💡 Dicas

- **Primeira execução pode levar 5-10 minutos** para fazer download das imagens e build
- **Sempre use `docker-compose down`** antes de `git pull` para evitar conflitos
- **Logs são seus amigos** - use `docker-compose logs -f` para troubleshooting
- **Não precisa reiniciar manualmente** - os containers têm `restart: unless-stopped`

---

## 🆘 Precisa de Ajuda?

1. Verifique os [logs](#ver-logs-em-tempo-real)
2. Tente [limpar tudo](#limpar-tudo-remover-volumes) e começar novamente
3. Verifique o [troubleshooting](#-troubleshooting)
4. Procure por issues similares no repositório

---

**Happy coding! 🎉**
