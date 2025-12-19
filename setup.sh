#!/bin/bash

# CNAB Project Setup Script
# This script helps set up the development environment

set -e

echo "🚀 CNAB Transaction Manager - Setup Script"
echo "==========================================="

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker and try again."
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose is not installed. Please install Docker Compose and try again."
    exit 1
fi

echo "✅ Docker and Docker Compose are installed"
echo ""

# Start Docker Compose
echo "📦 Starting Docker Compose services..."
docker-compose up -d

echo ""
echo "⏳ Waiting for services to be ready..."
sleep 10

echo ""
echo "✅ Setup Complete!"
echo ""
echo "Services are now running:"
echo "  - Frontend: http://localhost:3000"
echo "  - API: http://localhost:5000"
echo "  - API Swagger: http://localhost:5000/swagger"
echo "  - Database: localhost:5432"
echo ""
echo "To stop the services, run: docker-compose down"
