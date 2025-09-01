#!/bin/bash

# Deploy script for Production environment
# Usage: ./deploy-prod.sh

set -e

echo "🚀 Starting NFe Infrastructure deployment for PRODUCTION environment..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if production profile is configured
if ! aws sts get-caller-identity --profile production > /dev/null 2>&1; then
    echo -e "${RED}❌ AWS CLI profile 'production' não está configurado${NC}"
    echo "Configure com: aws configure --profile production"
    exit 1
fi

# Check if CDK is installed
if ! command -v cdk &> /dev/null; then
    echo -e "${RED}❌ AWS CDK não está instalado${NC}"
    echo "Instale com: npm install -g aws-cdk"
    exit 1
fi

# Production safety checks
echo -e "${RED}⚠️  AVISO: Você está fazendo deploy para PRODUÇÃO!${NC}"
echo -e "${YELLOW}📋 Verificações de segurança para produção:${NC}"

# Check for staging deployment first
echo -e "${BLUE}❓ Você testou esta versão em ambiente de staging? (y/N)${NC}"
read -r staging_confirmation
if [[ ! "$staging_confirmation" =~ ^[Yy]$ ]]; then
    echo -e "${RED}❌ Por favor, teste em staging primeiro${NC}"
    exit 1
fi

# Check for backup
echo -e "${BLUE}❓ Você fez backup dos dados críticos? (y/N)${NC}"
read -r backup_confirmation
if [[ ! "$backup_confirmation" =~ ^[Yy]$ ]]; then
    echo -e "${RED}❌ Faça backup dos dados críticos antes de continuar${NC}"
    exit 1
fi

# Check for maintenance window
echo -e "${BLUE}❓ Este deployment está sendo feito durante uma janela de manutenção? (y/N)${NC}"
read -r maintenance_confirmation
if [[ ! "$maintenance_confirmation" =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}⚠️  Considere agendar uma janela de manutenção${NC}"
fi

echo -e "${BLUE}📋 Verificando pré-requisitos...${NC}"

# Install dependencies
echo -e "${YELLOW}📦 Instalando dependências...${NC}"
npm install

# Build the project
echo -e "${YELLOW}🔨 Construindo o projeto...${NC}"
npm run build

# Check if .NET applications are built for production
if [ ! -d "../NFe.API/bin/Release/net8.0/publish" ]; then
    echo -e "${YELLOW}🔨 Construindo NFe.API para produção...${NC}"
    cd ../NFe.API
    dotnet publish -c Release -o bin/Release/net8.0/publish --self-contained false
    cd ../infrastructure
fi

if [ ! -d "../NFe.Worker/bin/Release/net8.0/publish" ]; then
    echo -e "${YELLOW}🔨 Construindo NFe.Worker para produção...${NC}"
    cd ../NFe.Worker
    dotnet publish -c Release -o bin/Release/net8.0/publish --self-contained false
    cd ../infrastructure
fi

# Bootstrap CDK for production (if needed)
echo -e "${YELLOW}🎯 Verificando bootstrap do CDK para produção...${NC}"
cdk bootstrap --profile production || true

# Synthesize the stack
echo -e "${YELLOW}🔍 Sintetizando a stack de produção...${NC}"
npm run synth

# Show diff before deployment
echo -e "${YELLOW}📊 Mostrando diferenças para produção...${NC}"
npm run diff:prod || true

# Final confirmation with timeout
echo -e "${RED}⚠️  CONFIRMAÇÃO FINAL DE PRODUÇÃO ⚠️${NC}"
echo -e "${BLUE}❓ Tem CERTEZA que deseja fazer deploy para PRODUÇÃO? Digite 'DEPLOY-PROD' para confirmar:${NC}"
read -r final_confirmation
if [[ "$final_confirmation" != "DEPLOY-PROD" ]]; then
    echo -e "${YELLOW}⚠️  Deployment cancelado. Confirmação incorreta.${NC}"
    exit 0
fi

# Deploy the stack
echo -e "${GREEN}🚀 Fazendo deploy da infraestrutura para PRODUÇÃO...${NC}"
npm run deploy:prod

# Get stack outputs
echo -e "${GREEN}📋 Outputs da stack de produção:${NC}"
aws cloudformation describe-stacks \
    --stack-name NFeInfrastructureStack-prod \
    --query 'Stacks[0].Outputs[*].[OutputKey,OutputValue,Description]' \
    --output table \
    --profile production

# Post-deployment checks
echo -e "${YELLOW}🔍 Executando verificações pós-deploy...${NC}"

# Check API health
API_URL=$(aws cloudformation describe-stacks \
    --stack-name NFeInfrastructureStack-prod \
    --query 'Stacks[0].Outputs[?OutputKey==`ApiUrl`].OutputValue' \
    --output text \
    --profile production)

if [ ! -z "$API_URL" ]; then
    echo -e "${BLUE}🔍 Testando API de produção...${NC}"
    sleep 30 # Wait for Lambda cold start
    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}health" || echo "000")
    if [ "$HTTP_STATUS" = "200" ]; then
        echo -e "${GREEN}✅ API de produção respondendo corretamente${NC}"
    else
        echo -e "${RED}❌ API de produção com problemas (HTTP $HTTP_STATUS)${NC}"
        echo -e "${YELLOW}⚠️  Verifique os logs do CloudWatch${NC}"
    fi
fi

echo -e "${GREEN}✅ Deploy de produção concluído!${NC}"
echo -e "${BLUE}📝 Próximos passos críticos:${NC}"
echo "1. ✅ Monitore os dashboards do CloudWatch"
echo "2. ✅ Verifique os logs da aplicação"
echo "3. ✅ Teste os fluxos críticos de negócio"
echo "4. ✅ Verifique os alertas do SNS"
echo "5. ✅ Configure backup automático do RDS"
echo "6. ✅ Documente esta versão deployada"

echo -e "${GREEN}🎉 Ambiente PRODUÇÃO está operacional!${NC}"
echo -e "${YELLOW}📞 Em caso de problemas, execute o rollback imediatamente${NC}"