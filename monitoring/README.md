# Monitoring Setup - Prometheus & Grafana

This directory contains configuration files for monitoring the CNAB API using Prometheus and Grafana.

## Architecture

```
┌─────────────┐
│   CNAB API  │───/metrics───┐
└─────────────┘              │
                             ▼
                      ┌──────────────┐
                      │  Prometheus  │
                      │  (Scraper)   │
                      └──────┬───────┘
                             │
                             ▼
                      ┌──────────────┐
                      │   Grafana    │
                      │ (Dashboards) │
                      └──────────────┘
```

## Quick Start

### 1. Start Services

```bash
# Start all services including Prometheus and Grafana
docker-compose up -d

# Or start only monitoring services
docker-compose up -d prometheus grafana
```

### 2. Access Dashboards

- **Grafana**: http://localhost:3001
  - Username: `admin` (default)
  - Password: `admin` (default, change in production!)
- **Prometheus**: http://localhost:9090

### 3. View Metrics

- **API Metrics**: http://localhost:5000/metrics
- **Prometheus UI**: http://localhost:9090/graph

## Configuration

### Prometheus (`monitoring/prometheus/prometheus.yml`)

Configures:
- Scrape interval: 15 seconds
- Targets: API, PostgreSQL, Redis
- Retention: Default (15 days)

### Grafana

**Data Sources** (`monitoring/grafana/provisioning/datasources/prometheus.yml`):
- Automatically configured Prometheus data source
- URL: `http://prometheus:9090`

**Dashboards** (`monitoring/grafana/provisioning/dashboards/dashboard.yml`):
- Auto-provisioned from `monitoring/grafana/dashboards/`
- Refresh interval: 10 seconds

## Available Dashboards

### 1. CNAB API - Overview
- HTTP request rate
- Response time (p95)
- Status codes distribution
- Active connections
- Error rate

### 2. CNAB API - Detailed Metrics
- Request rate by endpoint
- Response time distribution (heatmap)
- Database connections
- Redis memory usage
- File uploads processing

### 3. CNAB API - Complete Monitoring
- Comprehensive view of all metrics
- Real-time monitoring
- Historical trends

### 4. CNAB API - With Filters (NEW)
- **Interactive filters** for:
  - Endpoint selection
  - Status code filtering
  - Upload status filtering
  - Error type filtering
- Filtered views of all metrics
- Dynamic query building

## Prometheus Queries

See [monitoring/prometheus/queries.md](prometheus/queries.md) for a comprehensive list of useful Prometheus queries with filters.

## Alerting

Prometheus alert rules are configured in `monitoring/prometheus/alert_rules.yml`:

- **High Error Rate**: Warning when error rate > 0.1 req/s
- **Critical Error Rate**: Critical when error rate > 1.0 req/s
- **High Response Time**: Warning when P95 > 2.0s
- **File Upload Failures**: Warning when failure rate > 0.05 uploads/s
- **Queue Backlog**: Warning when queue size > 100, Critical when > 500
- **Processing Errors**: Warning when error rate > 1.0 errors/s
- **Service Health**: Critical alerts for database, Redis, and API downtime

## Metrics Exposed

The API exposes the following Prometheus metrics:

### HTTP Metrics (from prometheus-net)
- `http_requests_received_total` - Total HTTP requests (with labels: `method`, `route`, `code`)
- `http_request_duration_seconds` - Request duration histogram (with labels: `method`, `route`)
- `http_requests_active` - Active HTTP connections

### Custom CNAB Metrics
- `cnab_file_uploads_total` - File upload count (label: `status`)
- `cnab_file_upload_size_bytes` - File upload size histogram (label: `status`)
- `cnab_file_upload_processing_duration_seconds` - File processing duration (label: `status`)
- `cnab_transactions_processed_total` - Transactions processed (labels: `type`, `status`)
- `cnab_transactions_in_database` - Current transaction count (gauge)
- `cnab_processing_queue_size` - Current queue size (gauge)
- `cnab_queue_operations_total` - Queue operations (labels: `operation`, `status`)
- `cnab_processing_errors_total` - Processing errors (labels: `error_type`, `component`)
- `cnab_api_operations_total` - API operations (labels: `endpoint`, `method`, `status`)

## Environment Variables

Add to `.env`:

```bash
# Prometheus
PROMETHEUS_PORT=9090

# Grafana
GRAFANA_PORT=3001
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=admin
```

## Troubleshooting

**Prometheus not scraping metrics:**
- Verify API is running: `curl http://localhost:5000/metrics`
- Check Prometheus targets: http://localhost:9090/targets
- Verify network connectivity in Docker

**Grafana dashboards empty:**
- Check data source connection: Configuration → Data Sources
- Verify Prometheus is accessible from Grafana container
- Check dashboard queries match available metrics

**Metrics not appearing:**
- Ensure API has `UsePrometheusMetrics()` in pipeline
- Verify `/metrics` endpoint is accessible
- Check Prometheus scrape configuration

**Dashboard "CNAB API - With Filters" not appearing:**
- Restart Grafana: `docker-compose restart grafana`
- Check logs: `docker-compose logs grafana | grep -i dashboard`
- Import manually: Dashboards → Import → Select `monitoring/grafana/dashboards/cnab-api-with-filters.json`
- See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for detailed steps

**Prometheus alerts not showing:**
- Reload Prometheus: `curl -X POST http://localhost:9090/-/reload`
- Or restart: `docker-compose restart prometheus`
- Check rules: http://localhost:9090/rules
- Check alerts: http://localhost:9090/alerts
- Validate rules: `docker-compose exec prometheus promtool check rules /etc/prometheus/alert_rules.yml`
- See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for detailed steps

## Production Considerations

1. **Security**:
   - Change default Grafana admin password
   - Use authentication for Prometheus
   - Restrict network access

2. **Retention**:
   - Configure Prometheus retention policy
   - Set up long-term storage (e.g., Thanos)

3. **Alerting**:
   - Configure Alertmanager for Prometheus
   - Set up alert rules in `monitoring/prometheus/alert_rules.yml`

4. **Scaling**:
   - Use Prometheus federation for multiple instances
   - Consider Grafana Cloud for managed monitoring

