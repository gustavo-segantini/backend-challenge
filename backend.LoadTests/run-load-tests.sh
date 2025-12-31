#!/bin/bash

# Script to run load tests with monitoring setup

set -e

echo "🚀 CNAB API Load Tests Runner"
echo "=============================="
echo ""

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Check if API is running
echo -e "${YELLOW}Checking if API is running...${NC}"
if ! curl -s http://localhost:5000/api/v1/health > /dev/null; then
    echo "❌ API is not running at http://localhost:5000"
    echo "Please start the API first: docker-compose up -d api"
    exit 1
fi
echo -e "${GREEN}✅ API is running${NC}"

# Check if test user exists (optional - will fail gracefully if not)
echo ""
echo -e "${YELLOW}Checking test user...${NC}"
echo "If authentication fails, create a user:"
echo "  curl -X POST http://localhost:5000/api/v1/auth/register \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"username\":\"loadtest@example.com\",\"password\":\"LoadTest123!\",\"role\":\"User\"}'"

# Check if Grafana is running (optional)
echo ""
if curl -s http://localhost:3001/api/health > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Grafana is running${NC}"
    echo "   Open http://localhost:3001 to monitor in real-time"
    echo "   Dashboard: CNAB API - Overview"
else
    echo -e "${YELLOW}⚠️  Grafana is not running (optional)${NC}"
    echo "   Start with: docker-compose up -d grafana"
fi

# Check if Prometheus is running (optional)
if curl -s http://localhost:9090/-/healthy > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Prometheus is running${NC}"
    echo "   Open http://localhost:9090 to view metrics"
else
    echo -e "${YELLOW}⚠️  Prometheus is not running (optional)${NC}"
    echo "   Start with: docker-compose up -d prometheus"
fi

echo ""
echo -e "${YELLOW}Starting load tests...${NC}"
echo ""

# Run the tests
cd "$(dirname "$0")"
dotnet run

echo ""
echo -e "${GREEN}✅ Load tests completed!${NC}"
echo ""
echo "Next steps:"
echo "  1. Review the results above"
echo "  2. Check Grafana dashboards for detailed metrics"
echo "  3. Review API logs: docker-compose logs -f api"
echo ""

