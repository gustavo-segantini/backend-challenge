# Como Executar e Acompanhar Testes de Carga

Guia completo para executar testes de carga e interpretar os resultados.

## 📋 Pré-requisitos

1. **API rodando**: Certifique-se de que a API está rodando em `http://localhost:5000`
2. **.NET 9 SDK**: Instalado e funcionando

**Nota**: Não é necessário criar usuário ou configurar nada! O processo é totalmente automático.

### ✨ Melhorias Recentes

✅ **Geração de arquivos únicos**: Cada teste gera arquivos CNAB únicos com múltiplas linhas (1-5) para evitar duplicatas  
✅ **Rate limiting ajustado**: API configurada para aceitar até 60 uploads/minuto (1 req/segundo)  
✅ **Suporte a arquivos de 1 linha**: Validação corrigida para aceitar arquivos com apenas 1 linha de transação  
✅ **Gerador baseado em Python**: Gerador C# convertido do gerador Python de referência, com nomes realistas e CPFs válidos

## 🚀 Passo a Passo

### Executar Testes (Zero Configuração!)

```bash
# Navegar para o diretório
cd backend.LoadTests

# Executar testes (primeira vez pode precisar restaurar pacotes)
dotnet run
```

**É isso!** O script irá:
1. ✅ Verificar se a API está acessível
2. ✅ Tentar fazer login com credenciais padrão
3. ✅ Criar o usuário automaticamente se não existir
4. ✅ Executar os testes

### Configuração Opcional

O arquivo `appsettings.json` é **opcional**. Se não existir, serão usados os valores padrão:

- **API URL**: `http://localhost:5000/api/v1`
- **Usuário**: `loadtest@example.com`
- **Senha**: `LoadTest123!`

**Para personalizar**, edite `appsettings.json`:

```json
{
  "LoadTest": {
    "ApiBaseUrl": "http://localhost:5000/api/v1",
    "TestUser": {
      "Username": "seu-usuario@example.com",
      "Password": "SuaSenha123!"
    }
  }
}
```

Mas **não é necessário** - funciona sem configuração!

```bash
# Navegar para o diretório
cd backend.LoadTests

# Restaurar pacotes (primeira vez)
dotnet restore

# Executar testes
dotnet run
```

## 📊 Interpretando os Resultados

O NBomber exibe resultados detalhados no console. Aqui está o que procurar:

### Exemplo de Saída

```
🚀 Starting CNAB API Load Tests
API Base URL: http://localhost:5000/api/v1

✅ Authentication successful

[Scenario: Health Check]
  ok: 300, fail: 0, RPS: 10.0
  min: 5ms, mean: 12ms, max: 45ms
  p50: 10ms, p75: 15ms, p95: 25ms, p99: 35ms

[Scenario: Get Uploads]
  ok: 300, fail: 0, RPS: 5.0
  min: 8ms, mean: 20ms, max: 120ms
  p50: 18ms, p75: 25ms, p95: 50ms, p99: 80ms
```

### Métricas Importantes

#### 1. **RPS (Requests Per Second)**
- **O que é**: Taxa de requisições por segundo
- **Bom**: Próximo do valor configurado (ex: 10 req/s configurado = ~10 RPS)
- **Ruim**: Muito menor que o configurado (indica gargalo)

#### 2. **Response Time (Tempo de Resposta)**
- **min**: Melhor caso
- **mean**: Média (não é muito confiável com outliers)
- **p50 (mediana)**: 50% das requisições são mais rápidas
- **p95**: 95% das requisições são mais rápidas (métrica mais importante!)
- **p99**: 99% das requisições são mais rápidas
- **max**: Pior caso

**Interpretação:**
- **p95 < 100ms**: Excelente
- **p95 < 500ms**: Bom
- **p95 > 1000ms**: Precisa investigar

#### 3. **Success/Failure Rate**
- **ok**: Requisições bem-sucedidas
- **fail**: Requisições que falharam
- **Taxa de sucesso**: `ok / (ok + fail) * 100`

**Interpretação:**
- **100% sucesso**: Ideal
- **> 99% sucesso**: Aceitável
- **< 95% sucesso**: Problema crítico

#### 4. **Data Transfer**
- **sent**: Bytes enviados
- **received**: Bytes recebidos
- Útil para identificar problemas de rede ou payloads grandes

## 📈 Acompanhamento em Tempo Real

### Opção 1: Grafana (Recomendado)

1. **Abra o Grafana**: http://localhost:3001
2. **Acesse o dashboard**: "CNAB API - Overview" ou "CNAB API - With Filters"
3. **Monitore durante o teste**:
   - **HTTP Request Rate**: Deve aumentar durante o teste
   - **Response Time (p95)**: Deve mostrar latência em tempo real
   - **Status Codes**: Verifique se há erros (5xx)
   - **Error Rate**: Deve permanecer baixo

### Opção 2: Prometheus

1. **Abra Prometheus**: http://localhost:9090
2. **Execute queries**:
   ```
   # Taxa de requisições
   rate(http_requests_received_total[1m])
   
   # Tempo de resposta p95
   histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[1m]))
   
   # Taxa de erros
   rate(http_requests_received_total{code=~"5.."}[1m])
   ```

### Opção 3: Logs da API

```bash
# Ver logs em tempo real
docker-compose logs -f api

# Ou se rodando localmente
# Os logs aparecerão no console onde a API está rodando
```

## 🔍 Análise Detalhada

### Cenários de Teste

#### 1. Health Check (30 segundos, 10 req/s)
- **Objetivo**: Verificar disponibilidade básica
- **Esperado**: 100% sucesso, p95 < 50ms
- **Problema se**: Falhas ou latência alta

#### 2. Get Uploads (60 segundos, 5 req/s)
- **Objetivo**: Testar consultas com paginação
- **Esperado**: 100% sucesso, p95 < 200ms
- **Problema se**: Timeouts ou erros 500

#### 3. Get Transactions (60 segundos, 5 req/s)
- **Objetivo**: Testar consultas de transações
- **Esperado**: 100% sucesso (ou 404 se não houver dados), p95 < 300ms
- **Problema se**: Erros 500 ou latência muito alta

#### 4. Upload CNAB File (120 segundos, 1 req/s)
- **Objetivo**: Testar upload de arquivos
- **Esperado**: 100% sucesso ou 202 Accepted, p95 < 2000ms
- **Problema se**: Timeouts ou erros de validação
- **Nota**: Os testes geram arquivos únicos com múltiplas linhas (1-5) para garantir que cada upload tenha um hash único
- **Nota**: Os testes geram arquivos únicos com múltiplas linhas (1-5) para evitar duplicatas

## 🎯 Dicas de Análise

### 1. Comparar Cenários
- Health Check deve ser o mais rápido
- Uploads devem ser mais lentos (processamento)
- Compare p95 entre cenários

### 2. Identificar Gargalos
- **RPS baixo**: API não consegue processar rápido o suficiente
- **p95 alto**: Algumas requisições estão muito lentas
- **Falhas**: Verifique logs da API para identificar causa

### 3. Monitorar Recursos
Durante os testes, monitore:
- **CPU**: Não deve estar em 100%
- **Memória**: Não deve esgotar
- **Database**: Conexões não devem esgotar
- **Redis**: Não deve ter problemas de conexão

### 4. Aumentar Carga Gradualmente
1. Comece com carga baixa (1-2 req/s)
2. Aumente gradualmente
3. Identifique o ponto de quebra
4. Documente os limites encontrados

## 🐛 Troubleshooting

### Erro: "Failed to authenticate"
- Verifique se o usuário existe
- Confirme credenciais no `appsettings.json`
- Teste login manualmente via Swagger

### Erro: "Connection refused"
- Verifique se a API está rodando
- Confirme a URL no `appsettings.json`
- Teste: `curl http://localhost:5000/api/v1/health`

### Alta Taxa de Falhas
- Verifique logs da API
- Confirme que database e Redis estão rodando
- Verifique rate limiting (API permite até 60 uploads/minuto - 1 req/segundo)
- Se receber 429 (Too Many Requests), reduza a frequência de uploads no teste

### Latência Alta
- Verifique recursos do sistema (CPU, memória)
- Monitore conexões do banco de dados
- Verifique se há processamento pesado em background

## 📝 Exemplo Completo de Execução

```bash
# 1. Criar usuário de teste
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"loadtest@example.com","password":"LoadTest123!","role":"User"}'

# 2. Abrir Grafana em outra janela
# http://localhost:3001

# 3. Executar testes
cd backend.LoadTests
dotnet run

# 4. Observar resultados no console e no Grafana
```

## 📊 Relatórios

O NBomber também gera relatórios HTML. Após a execução, procure por:
- Arquivos `.html` no diretório de saída
- Relatórios detalhados com gráficos e estatísticas

## 🔄 Próximos Passos

Após identificar problemas:
1. Analise os logs da API
2. Verifique métricas no Grafana
3. Ajuste configurações (rate limiting, timeouts, etc.)
4. Execute novamente para validar melhorias

