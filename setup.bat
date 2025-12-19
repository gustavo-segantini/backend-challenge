@echo off
REM CNAB Project Setup Script for Windows
REM This script helps set up the development environment

echo.
echo 🚀 CNAB Transaction Manager - Setup Script
echo ===========================================
echo.

REM Check if Docker is installed
where docker >nul 2>nul
if errorlevel 1 (
    echo ❌ Docker is not installed. Please install Docker Desktop and try again.
    pause
    exit /b 1
)

REM Check if Docker Compose is installed
where docker-compose >nul 2>nul
if errorlevel 1 (
    echo ❌ Docker Compose is not installed. Please install Docker Desktop and try again.
    pause
    exit /b 1
)

echo ✅ Docker and Docker Compose are installed
echo.

REM Start Docker Compose
echo 📦 Starting Docker Compose services...
docker-compose up -d

echo.
echo ⏳ Waiting for services to be ready (30 seconds)...
timeout /t 30 /nobreak

echo.
echo ✅ Setup Complete!
echo.
echo Services are now running:
echo   - Frontend: http://localhost:3000
echo   - API: http://localhost:5000
echo   - API Swagger: http://localhost:5000/swagger
echo   - Database: localhost:5432
echo.
echo To stop the services, run: docker-compose down
echo.
pause
