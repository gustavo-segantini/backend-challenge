# Quick Fix - Dashboard e Alertas

## Problema: Dashboard "CNAB API - With Filters" não aparece

### Solução Rápida:

1. **Reiniciar Grafana:**
   ```bash
   docker-compose restart grafana
   ```

2. **Aguardar 10 segundos e verificar:**
   - Acesse http://localhost:3001
   - Vá em Dashboards → Browse
   - Procure por "CNAB API - With Filters"

3. **Se ainda não aparecer, importar manualmente:**
   - Acesse http://localhost:3001
   - Vá em Dashboards → Import
   - Clique em "Upload JSON file"
   - Selecione: `monitoring/grafana/dashboards/cnab-api-with-filters.json`
   - Clique em "Load" e depois "Import"

## Problema: Alertas não aparecem no Prometheus

### Solução Rápida:

1. **Verificar se as regras estão carregadas:**
   ```bash
   # Acesse no navegador
   http://localhost:9090/rules
   ```

2. **Se não aparecer, recarregar Prometheus:**
   ```bash
   # Windows PowerShell
   Invoke-WebRequest -Uri http://localhost:9090/-/reload -Method POST
   
   # Ou reiniciar
   docker-compose restart prometheus
   ```

3. **Verificar alertas:**
   - Acesse http://localhost:9090/alerts
   - Deve mostrar os 11 alertas configurados

4. **Validar regras:**
   ```bash
   docker-compose exec prometheus promtool check rules /etc/prometheus/alert_rules.yml
   ```

## Verificação Rápida

```bash
# Verificar se Prometheus está rodando
docker-compose ps prometheus

# Verificar se Grafana está rodando
docker-compose ps grafana

# Ver logs do Prometheus
docker-compose logs prometheus | Select-Object -Last 20

# Ver logs do Grafana
docker-compose logs grafana | Select-Object -Last 20
```

## URLs Importantes

- **Prometheus Rules**: http://localhost:9090/rules
- **Prometheus Alerts**: http://localhost:9090/alerts
- **Grafana Dashboards**: http://localhost:3001/dashboards
- **Grafana Import**: http://localhost:3001/dashboard/import

