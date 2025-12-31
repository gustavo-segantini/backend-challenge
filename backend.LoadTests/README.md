# Load Tests - CNAB API

This project contains load testing scenarios for the CNAB API using [NBomber](https://nbomber.com/).

## Prerequisites

- .NET 9 SDK
- CNAB API running (default: `http://localhost:5000`)

**No configuration needed!** The test user is created automatically.

## Configuration (Optional)

The `appsettings.json` file is **optional**. Default values are used if not specified:

- `LoadTest:ApiBaseUrl` - API base URL (default: `http://localhost:5000/api/v1`)
- `LoadTest:TestUser:Username` - Test user username (default: `loadtest@example.com`)
- `LoadTest:TestUser:Password` - Test user password (default: `LoadTest123!`)

The test user will be **created automatically** if it doesn't exist.

## Running Load Tests

### Quick Start (Zero Configuration!)

```bash
# Navigate to load tests directory
cd backend.LoadTests

# Run load tests (first time may need to restore packages)
dotnet run
```

**That's it!** The script will:
1. ✅ Check if API is accessible
2. ✅ Try to login with default credentials
3. ✅ **Automatically create the test user** if it doesn't exist
4. ✅ Run all test scenarios

No manual user creation or configuration needed!

### Detailed Guide

See [HOW_TO_RUN.md](HOW_TO_RUN.md) for:
- Step-by-step instructions
- How to interpret results
- Real-time monitoring with Grafana
- Troubleshooting guide

## Test Scenarios

### 1. Health Check
- **Duration**: 30 seconds
- **Load**: 10 requests/second
- **Endpoint**: `GET /health`
- **Purpose**: Test basic API availability

### 2. Get Uploads
- **Duration**: 60 seconds
- **Load**: 5 requests/second
- **Endpoint**: `GET /transactions/uploads`
- **Purpose**: Test pagination and query performance

### 3. Get Transactions
- **Duration**: 60 seconds
- **Load**: 5 requests/second
- **Endpoint**: `GET /transactions/stores/{uploadId}`
- **Purpose**: Test transaction query performance

### 4. Upload CNAB File
- **Duration**: 120 seconds
- **Load**: 1 request/second
- **Endpoint**: `POST /transactions/upload`
- **Purpose**: Test file upload performance

## Understanding Results

NBomber provides detailed statistics including:

- **Request Rate (RPS)**: Requests per second
- **Response Time**: Min, mean, p50, p75, p95, p99, max
- **Success/Failure Rate**: Percentage of successful requests
- **Data Transfer**: Bytes sent/received

### Key Metrics to Watch

- **p95 Response Time**: 95% of requests are faster than this (most important!)
- **Success Rate**: Should be > 99%
- **RPS**: Should match configured rate (indicates no bottlenecks)

### Real-Time Monitoring

**Option 1: Grafana (Recommended)**
1. Open http://localhost:3001
2. Go to "CNAB API - Overview" dashboard
3. Monitor during test execution:
   - HTTP Request Rate
   - Response Time (p95)
   - Error Rate
   - Status Codes

**Option 2: Prometheus**
1. Open http://localhost:9090
2. Run queries:
   - `rate(http_requests_received_total[1m])`
   - `histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[1m]))`

See [HOW_TO_RUN.md](HOW_TO_RUN.md) for detailed interpretation guide.

## Tips

1. **Start with low load**: Gradually increase load to find breaking points
2. **Monitor resources**: Watch CPU, memory, and database connections during tests
3. **Use Grafana**: Monitor real-time metrics in Grafana dashboards during load tests
4. **Test in production-like environment**: Use similar hardware and network conditions

## Troubleshooting

**Authentication fails:**
- Ensure API is running: `curl http://localhost:5000/api/v1/health`
- Check API logs: `docker-compose logs api`
- The script automatically creates the user, but if it fails, check API logs

**API not accessible:**
- Verify API is running: `docker-compose ps api`
- Check if API is on a different port (edit `appsettings.json` if needed)
- Verify network connectivity

**High error rate:**
- Check API logs for errors
- Verify database and Redis are running
- Check rate limiting configuration

