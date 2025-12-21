#!/bin/bash

# CNAB Project Setup Script
# Works on macOS, Linux, and Windows (Git Bash / WSL)

set -e

echo "🚀 CNAB Transaction Manager - Setup Script"
echo "==========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo -e "${RED}❌ Docker is not installed.${NC}"
    echo "Please install Docker Desktop from: https://www.docker.com/products/docker-desktop"
    exit 1
fi

echo -e "${GREEN}✅ Docker is installed${NC}"

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo -e "${RED}❌ Docker Compose is not installed.${NC}"
    echo "Please ensure Docker Desktop includes Compose or install it separately."
    exit 1
fi

echo -e "${GREEN}✅ Docker Compose is installed${NC}"

# Check if Docker daemon is running
if ! docker ps &> /dev/null; then
    echo -e "${RED}❌ Docker daemon is not running.${NC}"
    echo "Please start Docker Desktop and try again."
    exit 1
fi

echo -e "${GREEN}✅ Docker daemon is running${NC}"
echo ""

# Create .env file if it doesn't exist
if [ ! -f .env ]; then
    echo -e "${YELLOW}📝 Creating .env file from .env.example...${NC}"
    cp .env.example .env
    echo -e "${GREEN}✅ .env file created${NC}"
else
    echo -e "${GREEN}✅ .env file already exists${NC}"
fi

echo ""
echo -e "${YELLOW}📦 Building and starting services...${NC}"
echo "(This may take a few minutes on first run)"
echo ""

# Start Docker Compose
docker-compose up -d --build

echo ""
echo -e "${YELLOW}⏳ Waiting for services to become healthy (30 seconds)...${NC}"
sleep 30

# Check if services are running
if docker-compose ps | grep -q "healthy"; then
    echo -e "${GREEN}✅ All services are healthy!${NC}"
else
    echo -e "${YELLOW}⚠️  Services may still be starting, checking logs...${NC}"
fi

echo ""
echo -e "${GREEN}✅ Setup Complete!${NC}"
echo ""
echo "📍 Services are now available at:"
echo "   🌐 Frontend:      ${YELLOW}http://localhost:3000${NC}"
echo "   🔌 API:           ${YELLOW}http://localhost:5000${NC}"
echo "   📖 Swagger Docs:  ${YELLOW}http://localhost:5000/swagger${NC}"
echo "   🐘 Database:      ${YELLOW}localhost:5432${NC} (user: postgres, password: postgres)"
echo ""
echo "📋 Useful commands:"
echo "   docker-compose logs -f api       # View API logs"
echo "   docker-compose logs -f frontend  # View Frontend logs"
echo "   docker-compose down              # Stop all services"
echo ""
echo "👉 Next steps:"
echo "   1. Open http://localhost:3000 in your browser"
echo "   2. Try uploading a CNAB file"
echo "   3. Check the API docs at http://localhost:5000/swagger"
echo ""
