#!/bin/bash

# Script de Deploy NFe API - AWS Lambda
# Autor: Sistema Automatizado
# Data: $(date +%Y-%m-%d)

set -e  # Para na primeira falha

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Função para logging
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Banner
echo "=========================================="
echo "🚀 NFe API - Deploy para AWS Lambda"
echo "=========================================="
echo ""

# Verificar se está no diretório correto
if [ ! -f "template.yaml" ]; then
    log_error "Arquivo template.yaml não encontrado!"
    log_error "Execute este script no diretório raiz do projeto."
    exit 1
fi

# Verificar se AWS CLI está instalado
if ! command -v aws &> /dev/null; then
    log_error "AWS CLI não está instalado!"
    log_error "Instale com: curl 'https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip' -o 'awscliv2.zip' && unzip awscliv2.zip && sudo ./aws/install"
    exit 1
fi

# Verificar se SAM CLI está instalado
if ! command -v sam &> /dev/null; then
    log_error "SAM CLI não está instalado!"
    log_error "Instale seguindo: https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html"
    exit 1
fi

# Verificar credenciais AWS
log_info "Verificando credenciais AWS..."
if ! aws sts get-caller-identity &> /dev/null; then
    log_error "Credenciais AWS não configuradas!"
    log_error "Configure com: aws configure"
    exit 1
fi

AWS_ACCOUNT=$(aws sts get-caller-identity --query Account --output text)
AWS_REGION=$(aws configure get region)
log_success "Conectado na conta: $AWS_ACCOUNT (região: $AWS_REGION)"

# Validar template SAM
log_info "Validando template SAM..."
if sam validate --template template.yaml; then
    log_success "Template válido!"
else
    log_error "Template inválido!"
    exit 1
fi

# Solicitar parâmetros do banco de dados
echo ""
echo "📋 Configuração do Banco PostgreSQL:"
read -p "Host do banco (ex: postgres.abc123.us-east-1.rds.amazonaws.com): " DB_HOST
read -p "Nome do banco [nfe_db]: " DB_NAME
DB_NAME=${DB_NAME:-nfe_db}
read -p "Username [nfe_user]: " DB_USERNAME  
DB_USERNAME=${DB_USERNAME:-nfe_user}
read -s -p "Password: " DB_PASSWORD
echo ""

if [[ -z "$DB_HOST" || -z "$DB_PASSWORD" ]]; then
    log_error "Host e password do banco são obrigatórios!"
    exit 1
fi

# Build
log_info "Executando build do projeto..."
if sam build --template template.yaml; then
    log_success "Build concluído com sucesso!"
else
    log_error "Falha no build!"
    exit 1
fi

# Stack name
STACK_NAME="nfe-api-stack"
echo ""
read -p "Nome da stack [$STACK_NAME]: " USER_STACK_NAME
STACK_NAME=${USER_STACK_NAME:-$STACK_NAME}

# Deploy
log_info "Iniciando deploy na AWS..."
log_info "Stack: $STACK_NAME"
log_info "Região: $AWS_REGION"
echo ""

sam deploy \
    --template-file .aws-sam/build/template.yaml \
    --stack-name "$STACK_NAME" \
    --capabilities CAPABILITY_IAM \
    --region "$AWS_REGION" \
    --parameter-overrides \
        DatabaseHost="$DB_HOST" \
        DatabaseName="$DB_NAME" \
        DatabaseUsername="$DB_USERNAME" \
        DatabasePassword="$DB_PASSWORD" \
    --no-confirm-changeset

if [ $? -eq 0 ]; then
    log_success "Deploy concluído com sucesso! 🎉"
    echo ""
    echo "=========================================="
    echo "📊 Informações do Deploy:"
    echo "=========================================="
    
    # Obter URL da API
    API_URL=$(aws cloudformation describe-stacks \
        --stack-name "$STACK_NAME" \
        --query 'Stacks[0].Outputs[?OutputKey==`NFeApiUrl`].OutputValue' \
        --output text \
        --region "$AWS_REGION")
    
    echo "🌐 URL da API: $API_URL"
    echo "🔗 Health Check: ${API_URL}health"
    echo "📋 Swagger: ${API_URL}swagger"
    echo "🏠 Home: $API_URL"
    echo ""
    
    # Teste rápido
    log_info "Executando teste rápido..."
    if curl -s "${API_URL}health" > /dev/null; then
        log_success "API está respondendo! ✅"
    else
        log_warning "API pode estar inicializando. Aguarde alguns segundos."
    fi
    
    echo ""
    echo "=========================================="
    echo "🎯 Próximos passos:"
    echo "=========================================="
    echo "1. Teste os endpoints principais:"
    echo "   GET $API_URL"
    echo "   GET ${API_URL}api/v1/vendas"
    echo "   GET ${API_URL}api/v1/protocolos"
    echo ""
    echo "2. Monitore os logs:"
    echo "   sam logs --stack-name $STACK_NAME --tail"
    echo ""
    echo "3. Para atualizar:"
    echo "   ./deploy.sh (execute novamente este script)"
    echo ""
    
else
    log_error "Falha no deploy!"
    exit 1
fi