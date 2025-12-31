@echo off
REM Script to run load tests with monitoring setup

echo.
echo  CNAB API Load Tests Runner
echo ==============================
echo.

REM Check if API is running
echo Checking if API is running...
curl -s http://localhost:5000/api/v1/health >nul 2>&1
if errorlevel 1 (
    echo API is not running at http://localhost:5000
    echo Please start the API first: docker-compose up -d api
    pause
    exit /b 1
)
echo API is running

REM Check if Grafana is running (optional)
echo.
curl -s http://localhost:3001/api/health >nul 2>&1
if errorlevel 1 (
    echo Grafana is not running (optional)
    echo Start with: docker-compose up -d grafana
) else (
    echo Grafana is running
    echo    Open http://localhost:3001 to monitor in real-time
    echo    Dashboard: CNAB API - Overview
)

REM Check if Prometheus is running (optional)
curl -s http://localhost:9090/-/healthy >nul 2>&1
if errorlevel 1 (
    echo Prometheus is not running (optional)
    echo Start with: docker-compose up -d prometheus
) else (
    echo Prometheus is running
    echo    Open http://localhost:9090 to view metrics
)

echo.
echo If authentication fails, create a test user:
echo   curl -X POST http://localhost:5000/api/v1/auth/register -H "Content-Type: application/json" -d "{\"username\":\"loadtest@example.com\",\"password\":\"LoadTest123!\",\"role\":\"User\"}"
echo.

echo Starting load tests...
echo.

REM Run the tests
cd /d "%~dp0"
dotnet run

echo.
echo Load tests completed!
echo.
echo Next steps:
echo   1. Review the results above
echo   2. Check Grafana dashboards for detailed metrics
echo   3. Review API logs: docker-compose logs -f api
echo.
pause

