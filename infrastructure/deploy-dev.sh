#!/bin/bash

# Deploy script for Development environment
# Usage: ./deploy-dev.sh

set -e

echo "🚀 Starting NFe Infrastructure deployment for DEVELOPMENT environment..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if AWS CLI is configured
if ! aws sts get-caller-identity > /dev/null 2>&1; then
    echo -e "${RED}❌ AWS CLI não está configurado ou credenciais inválidas${NC}"
    echo "Configure com: aws configure"
    exit 1
fi

# Check if CDK is installed
if ! command -v cdk &> /dev/null; then
    echo -e "${RED}❌ AWS CDK não está instalado${NC}"
    echo "Instale com: npm install -g aws-cdk"
    exit 1
fi

echo -e "${BLUE}📋 Verificando pré-requisitos...${NC}"

# Install dependencies
echo -e "${YELLOW}📦 Instalando dependências...${NC}"
npm install

# Build the project
echo -e "${YELLOW}🔨 Construindo o projeto...${NC}"
npm run build

# Check if .NET applications are built
if [ ! -d "../NFe.API/bin/Release/net8.0/publish" ]; then
    echo -e "${YELLOW}🔨 Construindo NFe.API...${NC}"
    cd ../NFe.API
    dotnet publish -c Release -o bin/Release/net8.0/publish
    cd ../infrastructure
fi

if [ ! -d "../NFe.Worker/bin/Release/net8.0/publish" ]; then
    echo -e "${YELLOW}🔨 Construindo NFe.Worker...${NC}"
    cd ../NFe.Worker
    dotnet publish -c Release -o bin/Release/net8.0/publish
    cd ../infrastructure
fi

# Bootstrap CDK (if needed)
echo -e "${YELLOW}🎯 Verificando bootstrap do CDK...${NC}"
cdk bootstrap --profile default || true

# Synthesize the stack
echo -e "${YELLOW}🔍 Sintetizando a stack...${NC}"
npm run synth

# Show diff before deployment
echo -e "${YELLOW}📊 Mostrando diferenças...${NC}"
npm run diff:dev || true

# Confirm deployment
echo -e "${BLUE}❓ Deseja prosseguir com o deployment para DEV? (y/N)${NC}"
read -r confirmation
if [[ ! "$confirmation" =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}⚠️  Deployment cancelado pelo usuário${NC}"
    exit 0
fi

# Deploy the stack
echo -e "${GREEN}🚀 Fazendo deploy da infraestrutura para DEV...${NC}"
npm run deploy:dev

# Get stack outputs
echo -e "${GREEN}📋 Outputs da stack:${NC}"
aws cloudformation describe-stacks \
    --stack-name NFeInfrastructureStack-dev \
    --query 'Stacks[0].Outputs[*].[OutputKey,OutputValue,Description]' \
    --output table \
    --profile default

echo -e "${GREEN}✅ Deploy concluído com sucesso!${NC}"
echo -e "${BLUE}📝 Próximos passos:${NC}"
echo "1. Configure os certificados A1 no AWS Secrets Manager"
echo "2. Execute as migrations do banco de dados"
echo "3. Teste os endpoints da API"
echo "4. Configure monitoramento adicional se necessário"

echo -e "${GREEN}🎉 Ambiente DEV está pronto para uso!${NC}"