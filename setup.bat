@echo off
REM CNAB Project Setup Script for Windows
REM This script helps set up the development environment

echo.
echo  CNAB Transaction Manager - Setup Script
echo ===========================================
echo.

REM Check if Docker is installed
where docker >nul 2>nul
if errorlevel 1 (
    echo  Docker is not installed.
    echo Please install Docker Desktop from: https://www.docker.com/products/docker-desktop
    pause
    exit /b 1
)

echo Docker is installed

REM Check if Docker Compose is installed
where docker-compose >nul 2>nul
if errorlevel 1 (
    docker compose version >nul 2>nul
    if errorlevel 1 (
        echo Docker Compose is not installed.
        echo Please ensure Docker Desktop includes Compose.
        pause
        exit /b 1
    )
)

echo Docker Compose is installed

REM Check if Docker daemon is running
docker ps >nul 2>nul
if errorlevel 1 (
    echo Docker daemon is not running.
    echo Please start Docker Desktop and try again.
    pause
    exit /b 1
)

echo Docker daemon is running
echo.

REM Create .env file if it doesn't exist
if not exist .env (
    echo Creating .env file from .env.example...
    copy .env.example .env >nul
    echo .env file created
) else (
    echo .env file already exists
)   

echo.
echo Verifying monitoring configuration...

REM Verify monitoring directories exist
if not exist "monitoring\prometheus" (
    echo Error: monitoring\prometheus directory not found
    pause
    exit /b 1
)

if not exist "monitoring\grafana" (
    echo Error: monitoring\grafana directory not found
    pause
    exit /b 1
)

if not exist "monitoring\prometheus\prometheus.yml" (
    echo Error: monitoring\prometheus\prometheus.yml not found
    pause
    exit /b 1
)

if not exist "monitoring\prometheus\alert_rules.yml" (
    echo Error: monitoring\prometheus\alert_rules.yml not found
    pause
    exit /b 1
)

if not exist "monitoring\grafana\dashboards" (
    echo Error: monitoring\grafana\dashboards directory not found
    pause
    exit /b 1
)

if not exist "monitoring\grafana\provisioning" (
    echo Error: monitoring\grafana\provisioning directory not found
    pause
    exit /b 1
)

echo Monitoring configuration verified

echo.
echo Building and starting services...
echo (This may take a few minutes on first run)
echo.

REM Start Docker Compose with build
docker-compose up -d --build

echo.
echo Waiting for services to become healthy (15 seconds)...
timeout /t 15 /nobreak

REM Wait a bit more for Prometheus and Grafana to fully initialize
echo.
echo Waiting for monitoring services to initialize (5 seconds)...
timeout /t 5 /nobreak

REM Verify Prometheus is accessible and reload configuration
echo.
echo Verifying Prometheus...
curl -s http://localhost:9090/-/healthy >nul 2>&1
if errorlevel 1 (
    echo Prometheus may still be starting...
) else (
    echo Prometheus is running
    echo Reloading Prometheus configuration...
    powershell -Command "Invoke-WebRequest -Uri http://localhost:9090/-/reload -Method POST" >nul 2>&1
    echo Prometheus configuration reloaded
)

REM Verify Grafana is accessible
echo Verifying Grafana...
curl -s http://localhost:3001/api/health >nul 2>&1
if errorlevel 1 (
    echo Grafana may still be starting...
) else (
    echo Grafana is running
)

echo.
echo Setup Complete!
echo.
echo Services are now available at:
echo    Frontend:      http://localhost:3000
echo    API:           http://localhost:5000
echo    Swagger Docs:  http://localhost:5000/swagger
echo    Database:      localhost:5432 (user: postgres, password: postgres)
echo.
echo Monitoring ^& Metrics:
echo    Prometheus:     http://localhost:9090
echo    Prometheus Rules: http://localhost:9090/rules
echo    Prometheus Alerts: http://localhost:9090/alerts
echo    Grafana:       http://localhost:3001 (admin/admin)
echo    API Metrics:   http://localhost:5000/metrics
echo.
echo Useful commands:
echo    docker-compose logs -f api       # View API logs
echo    docker-compose logs -f frontend  # View Frontend logs
echo    docker-compose logs -f prometheus # View Prometheus logs
echo    docker-compose logs -f grafana   # View Grafana logs
echo    docker-compose down              # Stop all services
echo.
echo Next steps:
echo    1. Open http://localhost:3000 in your browser
echo    2. Try uploading a CNAB file
echo    3. Check the API docs at http://localhost:5000/swagger
echo    4. View metrics in Grafana: http://localhost:3001
echo    5. Check Prometheus alerts: http://localhost:9090/alerts
echo.
pause
