#!/bin/bash

# CNAB Project Setup Script
# Works on macOS, Linux, and Windows (Git Bash / WSL)

set -e

echo "CNAB Transaction Manager - Setup Script"
echo "==========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Docker is not installed.${NC}"
    echo "Please install Docker Desktop from: https://www.docker.com/products/docker-desktop"
    exit 1
fi

echo -e "${GREEN}Docker is installed${NC}"

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo -e "${RED}Docker Compose is not installed.${NC}"
    echo "Please ensure Docker Desktop includes Compose or install it separately."
    exit 1
fi

echo -e "${GREEN}Docker Compose is installed${NC}"

# Check if Docker daemon is running
if ! docker ps &> /dev/null; then
    echo -e "${RED}Docker daemon is not running.${NC}"
    echo "Please start Docker Desktop and try again."
    exit 1
fi

echo -e "${GREEN}Docker daemon is running${NC}"
echo ""

# Create .env file if it doesn't exist
if [ ! -f .env ]; then
    echo -e "${YELLOW}Creating .env file from .env.example...${NC}"
    cp .env.example .env
    echo -e "${GREEN}.env file created${NC}"
else
    echo -e "${GREEN}.env file already exists${NC}"
fi

# Verify monitoring directories exist
echo ""
echo -e "${YELLOW}Verifying monitoring configuration...${NC}"

if [ ! -d "monitoring/prometheus" ]; then
    echo -e "${RED}Error: monitoring/prometheus directory not found${NC}"
    exit 1
fi

if [ ! -d "monitoring/grafana" ]; then
    echo -e "${RED}Error: monitoring/grafana directory not found${NC}"
    exit 1
fi

if [ ! -f "monitoring/prometheus/prometheus.yml" ]; then
    echo -e "${RED}Error: monitoring/prometheus/prometheus.yml not found${NC}"
    exit 1
fi

if [ ! -f "monitoring/prometheus/alert_rules.yml" ]; then
    echo -e "${RED}Error: monitoring/prometheus/alert_rules.yml not found${NC}"
    exit 1
fi

if [ ! -d "monitoring/grafana/dashboards" ]; then
    echo -e "${RED}Error: monitoring/grafana/dashboards directory not found${NC}"
    exit 1
fi

if [ ! -d "monitoring/grafana/provisioning" ]; then
    echo -e "${RED}Error: monitoring/grafana/provisioning directory not found${NC}"
    exit 1
fi

echo -e "${GREEN}Monitoring configuration verified${NC}"

echo ""
echo -e "${YELLOW}Building and starting services...${NC}"
echo "(This may take a few minutes on first run)"
echo ""

# Start Docker Compose
docker-compose up -d --build

echo ""
echo -e "${YELLOW}Waiting for services to become healthy 15 seconds)...${NC}"
sleep 15

# Check if services are running
if docker-compose ps | grep -q "healthy"; then
    echo -e "${GREEN}All services are healthy!${NC}"
else
    echo -e "${YELLOW}Services may still be starting, checking logs...${NC}"
fi

# Wait a bit more for Prometheus and Grafana to fully initialize
echo ""
echo -e "${YELLOW}Waiting for monitoring services to initialize (5 seconds)...${NC}"
sleep 5

# Verify Prometheus is accessible
if curl -s http://localhost:9090/-/healthy > /dev/null 2>&1; then
    echo -e "${GREEN}Prometheus is running${NC}"
    
    # Reload Prometheus configuration to ensure alerts are loaded
    echo -e "${YELLOW}Reloading Prometheus configuration...${NC}"
    curl -s -X POST http://localhost:9090/-/reload > /dev/null 2>&1
    echo -e "${GREEN}Prometheus configuration reloaded${NC}"
else
    echo -e "${YELLOW}Prometheus may still be starting...${NC}"
fi

# Verify Grafana is accessible
if curl -s http://localhost:3001/api/health > /dev/null 2>&1; then
    echo -e "${GREEN}Grafana is running${NC}"
else
    echo -e "${YELLOW}Grafana may still be starting...${NC}"
fi

echo ""
echo -e "${GREEN}Setup Complete!${NC}"
echo ""
echo " Services are now available at:"
echo "    Frontend:      ${YELLOW}http://localhost:3000${NC}"
echo "    API:           ${YELLOW}http://localhost:5000${NC}"
echo "    Swagger Docs:  ${YELLOW}http://localhost:5000/swagger${NC}"
echo "    Database:      ${YELLOW}localhost:5432${NC} (user: postgres, password: postgres)"
echo ""
echo " Monitoring & Metrics:"
echo "    Prometheus:     ${YELLOW}http://localhost:9090${NC}"
echo "    Prometheus Rules: ${YELLOW}http://localhost:9090/rules${NC}"
echo "    Prometheus Alerts: ${YELLOW}http://localhost:9090/alerts${NC}"
echo "    Grafana:       ${YELLOW}http://localhost:3001${NC} (admin/admin)"
echo "    API Metrics:   ${YELLOW}http://localhost:5000/metrics${NC}"
echo ""
echo " Useful commands:"
echo "   docker-compose logs -f api       # View API logs"
echo "   docker-compose logs -f frontend  # View Frontend logs"
echo "   docker-compose logs -f prometheus # View Prometheus logs"
echo "   docker-compose logs -f grafana   # View Grafana logs"
echo "   docker-compose down              # Stop all services"
echo ""
echo " Next steps:"
echo "   1. Open http://localhost:3000 in your browser"
echo "   2. Try uploading a CNAB file"
echo "   3. Check the API docs at http://localhost:5000/swagger"
echo "   4. View metrics in Grafana: http://localhost:3001"
echo "   5. Check Prometheus alerts: http://localhost:9090/alerts"
echo ""
