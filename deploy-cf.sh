#!/bin/bash

# Script de Deploy NFe API - AWS Lambda via CloudFormation
# Autor: Sistema Automatizado

set -e

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

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
echo "🚀 NFe API - Deploy CloudFormation"
echo "=========================================="
echo ""

# Verificações básicas
if [ ! -f "cloudformation-template.yaml" ]; then
    log_error "Arquivo cloudformation-template.yaml não encontrado!"
    exit 1
fi

if ! command -v aws &> /dev/null; then
    log_error "AWS CLI não está instalado!"
    exit 1
fi

# Verificar credenciais AWS
log_info "Verificando credenciais AWS..."
if ! aws sts get-caller-identity &> /dev/null; then
    log_error "Credenciais AWS não configuradas!"
    exit 1
fi

AWS_ACCOUNT=$(aws sts get-caller-identity --query Account --output text)
AWS_REGION=$(aws configure get region)
log_success "Conectado na conta: $AWS_ACCOUNT (região: $AWS_REGION)"

# Parâmetros
STACK_NAME="nfe-api-stack"
FUNCTION_NAME="nfe-api-function"

echo ""
echo "📋 Configuração do Deploy:"
read -p "Nome da stack [$STACK_NAME]: " USER_STACK_NAME
STACK_NAME=${USER_STACK_NAME:-$STACK_NAME}

read -p "Nome da função Lambda [$FUNCTION_NAME]: " USER_FUNCTION_NAME
FUNCTION_NAME=${USER_FUNCTION_NAME:-$FUNCTION_NAME}

echo ""
echo "📋 Configuração do Banco PostgreSQL:"
read -p "Host do banco (deixe vazio para usar mock): " DB_HOST
read -p "Nome do banco [nfe_db]: " DB_NAME
DB_NAME=${DB_NAME:-nfe_db}
read -p "Username [nfe_user]: " DB_USERNAME  
DB_USERNAME=${DB_USERNAME:-nfe_user}
read -s -p "Password (deixe vazio para usar mock): " DB_PASSWORD
echo ""

# Deploy CloudFormation
log_info "Criando/atualizando stack CloudFormation..."

PARAMS="ParameterKey=FunctionName,ParameterValue=$FUNCTION_NAME"
PARAMS="$PARAMS ParameterKey=DatabaseHost,ParameterValue=${DB_HOST:-localhost}"
PARAMS="$PARAMS ParameterKey=DatabaseName,ParameterValue=$DB_NAME"
PARAMS="$PARAMS ParameterKey=DatabaseUsername,ParameterValue=$DB_USERNAME"
PARAMS="$PARAMS ParameterKey=DatabasePassword,ParameterValue=${DB_PASSWORD:-mock_password}"

# Verificar se stack existe
STACK_EXISTS=$(aws cloudformation describe-stacks --stack-name "$STACK_NAME" --query 'Stacks[0].StackStatus' --output text 2>/dev/null || echo "DOES_NOT_EXIST")

if [ "$STACK_EXISTS" = "DOES_NOT_EXIST" ]; then
    log_info "Criando nova stack..."
    aws cloudformation create-stack \
        --stack-name "$STACK_NAME" \
        --template-body file://cloudformation-template.yaml \
        --parameters $PARAMS \
        --capabilities CAPABILITY_NAMED_IAM \
        --region "$AWS_REGION"
    
    log_info "Aguardando criação da stack..."
    aws cloudformation wait stack-create-complete \
        --stack-name "$STACK_NAME" \
        --region "$AWS_REGION"
else
    log_info "Atualizando stack existente..."
    aws cloudformation update-stack \
        --stack-name "$STACK_NAME" \
        --template-body file://cloudformation-template.yaml \
        --parameters $PARAMS \
        --capabilities CAPABILITY_NAMED_IAM \
        --region "$AWS_REGION" || log_warning "Nenhuma alteração detectada na stack"
    
    log_info "Aguardando atualização da stack..."
    aws cloudformation wait stack-update-complete \
        --stack-name "$STACK_NAME" \
        --region "$AWS_REGION" 2>/dev/null || true
fi

log_success "Stack CloudFormation criada/atualizada com sucesso!"

# Obter informações da stack
API_URL=$(aws cloudformation describe-stacks \
    --stack-name "$STACK_NAME" \
    --query 'Stacks[0].Outputs[?OutputKey==`NFeApiUrl`].OutputValue' \
    --output text \
    --region "$AWS_REGION")

log_success "Deploy concluído! 🎉"
echo ""
echo "=========================================="
echo "📊 Informações do Deploy:"
echo "=========================================="
echo "🌐 URL da API: $API_URL"
echo "🔗 Health Check: ${API_URL}health"
echo "🏠 Home: $API_URL"
echo ""

# Teste básico
log_info "Testando API..."
if timeout 10 curl -s "${API_URL}" > /dev/null 2>&1; then
    log_success "API está respondendo! ✅"
else
    log_warning "API pode estar inicializando ou código precisa ser atualizado."
    log_info "Para atualizar o código da função Lambda:"
    log_info "1. Compile o projeto .NET localmente"
    log_info "2. Crie um ZIP do deployment"
    log_info "3. Execute: aws lambda update-function-code --function-name $FUNCTION_NAME --zip-file fileb://deployment.zip"
fi

echo ""
log_info "Stack criada com placeholder. Para deploy do código real, use ferramentas com .NET SDK instalado."