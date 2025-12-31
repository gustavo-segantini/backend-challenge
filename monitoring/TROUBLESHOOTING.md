# Troubleshooting - Grafana & Prometheus

## Dashboard não aparece no Grafana

### Problema: Dashboard "CNAB API - With Filters" não aparece

**Solução 1: Reiniciar o Grafana**
```bash
docker-compose restart grafana
```

**Solução 2: Verificar logs do Grafana**
```bash
docker-compose logs grafana | grep -i dashboard
```

**Solução 3: Verificar se os arquivos estão no lugar correto**
```bash
# Verificar estrutura de diretórios
ls -la monitoring/grafana/dashboards/
ls -la monitoring/grafana/provisioning/dashboards/
```

**Solução 4: Verificar configuração de provisionamento**
O arquivo `monitoring/grafana/provisioning/dashboards/dashboard.yml` deve apontar para:
```yaml
path: /var/lib/grafana/dashboards
```

E o volume no `docker-compose.yml` deve mapear:
```yaml
- ./monitoring/grafana/dashboards:/var/lib/grafana/dashboards
```

**Solução 5: Recarregar dashboards manualmente**
1. Acesse http://localhost:3001
2. Vá em Configuration → Provisioning → Dashboards
3. Clique em "Reload" se disponível

**Solução 6: Importar manualmente**
1. Acesse http://localhost:3001
2. Vá em Dashboards → Import
3. Selecione o arquivo `monitoring/grafana/dashboards/cnab-api-with-filters.json`

## Alertas não aparecem no Prometheus

### Problema: Nenhum alerta configurado aparece

**Solução 1: Verificar se o arquivo de regras existe**
```bash
ls -la monitoring/prometheus/alert_rules.yml
```

**Solução 2: Verificar se está referenciado no prometheus.yml**
O arquivo `monitoring/prometheus/prometheus.yml` deve ter:
```yaml
rule_files:
  - "alert_rules.yml"
```

**Solução 3: Reiniciar o Prometheus**
```bash
docker-compose restart prometheus
```

**Solução 4: Verificar logs do Prometheus**
```bash
docker-compose logs prometheus | grep -i alert
docker-compose logs prometheus | grep -i rule
```

**Solução 5: Verificar sintaxe do arquivo de regras**
```bash
# Validar YAML
docker-compose exec prometheus promtool check rules /etc/prometheus/alert_rules.yml
```

**Solução 6: Recarregar configuração do Prometheus**
```bash
# Via API
curl -X POST http://localhost:9090/-/reload
```

**Solução 7: Verificar se o Prometheus está lendo as regras**
1. Acesse http://localhost:9090/rules
2. Deve mostrar os grupos de alertas configurados

**Solução 8: Verificar se há erros de sintaxe**
O arquivo `alert_rules.yml` deve seguir o formato:
```yaml
groups:
  - name: cnab_api_alerts
    interval: 30s
    rules:
      - alert: HighErrorRate
        expr: rate(http_requests_received_total{code=~"5.."}[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High error rate detected"
```

## Verificar Status dos Serviços

```bash
# Ver status
docker-compose ps

# Ver logs do Prometheus
docker-compose logs prometheus

# Ver logs do Grafana
docker-compose logs grafana

# Verificar se o Prometheus está coletando métricas
curl http://localhost:9090/api/v1/targets

# Verificar se o Grafana está conectado ao Prometheus
# Acesse http://localhost:3001 → Configuration → Data Sources → Prometheus
```

## Reconstruir Containers

Se nada funcionar, reconstrua os containers:

```bash
# Parar serviços
docker-compose down

# Reconstruir e iniciar
docker-compose up -d --build prometheus grafana

# Aguardar inicialização
sleep 10

# Verificar logs
docker-compose logs prometheus grafana
```

