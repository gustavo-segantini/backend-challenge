# Environment Variables for Monitoring

Add these variables to your `.env` file to configure Prometheus and Grafana:

```bash
# Prometheus Configuration
PROMETHEUS_PORT=9090

# Grafana Configuration
GRAFANA_PORT=3001
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=admin  # Change in production!
```

## Default Values

If not specified, the following defaults are used:

- `PROMETHEUS_PORT`: `9090`
- `GRAFANA_PORT`: `3001`
- `GRAFANA_ADMIN_USER`: `admin`
- `GRAFANA_ADMIN_PASSWORD`: `admin`

## Production Recommendations

1. **Change Grafana Admin Password**: Always change the default password in production
2. **Use Strong Passwords**: Use complex passwords for Grafana admin account
3. **Restrict Network Access**: Consider using reverse proxy with authentication
4. **Enable HTTPS**: Use TLS/SSL for Grafana and Prometheus in production

## Example .env File

```bash
# Database
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=cnab_db

# API
API_PORT=5000
ASPNETCORE_ENVIRONMENT=Production

# Frontend
FRONTEND_PORT=3000

# MinIO
MINIO_ROOT_USER=minioadmin
MINIO_ROOT_PASSWORD=minioadmin

# Monitoring
PROMETHEUS_PORT=9090
GRAFANA_PORT=3001
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=your_secure_password_here

# JWT
JWT_SIGNING_KEY=your_jwt_signing_key_here_min_32_chars

# GitHub OAuth (optional)
GitHubOAuth__ClientId=your_github_client_id
GitHubOAuth__ClientSecret=your_github_client_secret
GitHubOAuth__CallbackUrl=http://localhost:5000/api/v1/auth/github/callback
```

