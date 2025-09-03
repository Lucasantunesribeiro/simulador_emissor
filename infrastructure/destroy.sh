#!/bin/bash

# Destroy script for NFe Infrastructure
# Usage: ./destroy.sh [dev|prod]

set -e

ENVIRONMENT=${1:-dev}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

if [ "$ENVIRONMENT" != "dev" ] && [ "$ENVIRONMENT" != "prod" ]; then
    echo -e "${RED}❌ Ambiente deve ser 'dev' ou 'prod'${NC}"
    echo "Uso: ./destroy.sh [dev|prod]"
    exit 1
fi

echo -e "${RED}⚠️  AVISO: Você está DESTRUINDO a infraestrutura do ambiente: $ENVIRONMENT${NC}"

# Additional warnings for production
if [ "$ENVIRONMENT" = "prod" ]; then
    echo -e "${RED}🚨 ATENÇÃO: DESTRUIÇÃO DO AMBIENTE DE PRODUÇÃO! 🚨${NC}"
    echo -e "${YELLOW}Isso irá DELETAR PERMANENTEMENTE:${NC}"
    echo "- ❌ Banco de dados PostgreSQL e TODOS os dados"
    echo "- ❌ Buckets S3 com XMLs, DANFEs e logs"
    echo "- ❌ Filas SQS e mensagens"
    echo "- ❌ Secrets Manager com certificados"
    echo "- ❌ Lambda functions e API Gateway"
    echo "- ❌ Monitoramento e alarms"
    echo -e "${RED}Esta ação é IRREVERSÍVEL!${NC}"
fi

# Check AWS credentials
PROFILE="default"
if [ "$ENVIRONMENT" = "prod" ]; then
    PROFILE="production"
fi

if ! aws sts get-caller-identity --profile $PROFILE > /dev/null 2>&1; then
    echo -e "${RED}❌ AWS CLI profile '$PROFILE' não está configurado${NC}"
    exit 1
fi

# List resources that will be destroyed
echo -e "${YELLOW}📋 Recursos que serão destruídos:${NC}"
aws cloudformation describe-stack-resources \
    --stack-name NFeInfrastructureStack-$ENVIRONMENT \
    --query 'StackResources[*].[ResourceType,LogicalResourceId]' \
    --output table \
    --profile $PROFILE 2>/dev/null || echo "Stack não encontrada ou não acessível"

# Data backup warning
if [ "$ENVIRONMENT" = "prod" ]; then
    echo -e "${BLUE}❓ Você fez backup de todos os dados críticos? (y/N)${NC}"
    read -r backup_confirmation
    if [[ ! "$backup_confirmation" =~ ^[Yy]$ ]]; then
        echo -e "${RED}❌ Faça backup dos dados antes de destruir a infraestrutura${NC}"
        exit 1
    fi
fi

# Final confirmation
echo -e "${RED}❓ Tem CERTEZA que deseja DESTRUIR o ambiente $ENVIRONMENT? Digite 'DESTROY-$ENVIRONMENT' para confirmar:${NC}"
read -r confirmation
if [[ "$confirmation" != "DESTROY-$ENVIRONMENT" ]]; then
    echo -e "${YELLOW}⚠️  Destruição cancelada. Confirmação incorreta.${NC}"
    exit 0
fi

# Additional confirmation for production
if [ "$ENVIRONMENT" = "prod" ]; then
    echo -e "${RED}❓ CONFIRMAÇÃO FINAL: Digite 'I-UNDERSTAND-THIS-WILL-DELETE-PRODUCTION':${NC}"
    read -r final_confirmation
    if [[ "$final_confirmation" != "I-UNDERSTAND-THIS-WILL-DELETE-PRODUCTION" ]]; then
        echo -e "${YELLOW}⚠️  Destruição cancelada. Confirmação final incorreta.${NC}"
        exit 0
    fi
fi

echo -e "${RED}💥 Iniciando destruição da infraestrutura...${NC}"

# Build project first
echo -e "${YELLOW}🔨 Construindo o projeto...${NC}"
npm run build

# Destroy the stack
if [ "$ENVIRONMENT" = "dev" ]; then
    npm run destroy:dev
else
    npm run destroy:prod
fi

echo -e "${GREEN}✅ Infraestrutura do ambiente $ENVIRONMENT foi destruída${NC}"

if [ "$ENVIRONMENT" = "prod" ]; then
    echo -e "${YELLOW}📝 Lembre-se de:${NC}"
    echo "- 📧 Notificar a equipe sobre a destruição"
    echo "- 📊 Remover dashboards e monitoramento externos"
    echo "- 🔐 Revogar acessos relacionados"
    echo "- 📋 Documentar o motivo da destruição"
fi

echo -e "${BLUE}🎯 Destruição concluída${NC}"