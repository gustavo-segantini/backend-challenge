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
    echo ❌ Docker is not installed.
    echo Please install Docker Desktop from: https://www.docker.com/products/docker-desktop
    pause
    exit /b 1
)

echo ✅ Docker is installed

REM Check if Docker Compose is installed
where docker-compose >nul 2>nul
if errorlevel 1 (
    docker compose version >nul 2>nul
    if errorlevel 1 (
        echo ❌ Docker Compose is not installed.
        echo Please ensure Docker Desktop includes Compose.
        pause
        exit /b 1
    )
)

echo ✅ Docker Compose is installed

REM Check if Docker daemon is running
docker ps >nul 2>nul
if errorlevel 1 (
    echo ❌ Docker daemon is not running.
    echo Please start Docker Desktop and try again.
    pause
    exit /b 1
)

echo ✅ Docker daemon is running
echo.

REM Create .env file if it doesn't exist
if not exist .env (
    echo 📝 Creating .env file from .env.example...
    copy .env.example .env >nul
    echo ✅ .env file created
) else (
    echo ✅ .env file already exists
)

echo.
echo 📦 Building and starting services...
echo (This may take a few minutes on first run)
echo.

REM Start Docker Compose with build
docker-compose up -d --build

echo.
echo ⏳ Waiting for services to become healthy (30 seconds)...
timeout /t 30 /nobreak

echo.
echo ✅ Setup Complete!
echo.
echo 📍 Services are now available at:
echo    🌐 Frontend:      http://localhost:3000
echo    🔌 API:           http://localhost:5000
echo    📖 Swagger Docs:  http://localhost:5000/swagger
echo    🐘 Database:      localhost:5432 (user: postgres, password: postgres)
echo.
echo 📋 Useful commands:
echo    docker-compose logs -f api       # View API logs
echo    docker-compose logs -f frontend  # View Frontend logs
echo    docker-compose down              # Stop all services
echo.
echo 👉 Next steps:
echo    1. Open http://localhost:3000 in your browser
echo    2. Try uploading a CNAB file
echo    3. Check the API docs at http://localhost:5000/swagger
echo.
pause
