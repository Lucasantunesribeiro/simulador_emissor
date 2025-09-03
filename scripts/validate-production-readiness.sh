#!/bin/bash

# Script de validação para ativação do ambiente de produção
# Autor: Sistema NFe
# Data: $(date +%Y-%m-%d)

set -e

echo "🔍 Validando preparação para produção NFe..."
echo "================================================"

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Função para log de sucesso
log_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

# Função para log de erro
log_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Função para log de warning
log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

# Função para log de info
log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

# Verificar se está no ambiente correto
if [ "$ASPNETCORE_ENVIRONMENT" != "Production" ]; then
    log_warning "ASPNETCORE_ENVIRONMENT não está definido como Production"
    log_info "Execute: export ASPNETCORE_ENVIRONMENT=Production"
fi

# 1. Verificar certificado A1 no AWS Secrets Manager
echo
log_info "1. Verificando certificado A1..."
if aws secretsmanager describe-secret --secret-id "${AWS_CERTIFICATE_SECRET_NAME}" &>/dev/null; then
    log_success "Certificado A1 encontrado no Secrets Manager"
    
    # Verificar se o certificado não está expirado
    cert_info=$(aws secretsmanager get-secret-value --secret-id "${AWS_CERTIFICATE_SECRET_NAME}" --query 'SecretString' --output text)
    if [ $? -eq 0 ]; then
        log_success "Certificado A1 acessível"
    else
        log_error "Erro ao acessar certificado A1"
        exit 1
    fi
else
    log_error "Certificado A1 não encontrado no Secrets Manager"
    log_error "Configure: ${AWS_CERTIFICATE_SECRET_NAME}"
    exit 1
fi

# 2. Verificar conectividade SEFAZ Produção
echo
log_info "2. Verificando conectividade SEFAZ produção..."
sefaz_url="https://nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx"

if curl -s --connect-timeout 10 --max-time 30 "$sefaz_url" > /dev/null; then
    log_success "SEFAZ produção acessível: $sefaz_url"
else
    log_error "Falha na conectividade com SEFAZ produção"
    log_error "URL: $sefaz_url"
    exit 1
fi

# 3. Verificar banco de dados
echo
log_info "3. Verificando conexão com banco de dados..."
if [ -z "$PROD_DB_HOST" ]; then
    log_error "PROD_DB_HOST não definido"
    exit 1
fi

# Test database connection (simplified check)
if nc -z "$PROD_DB_HOST" 5432; then
    log_success "Banco de dados acessível: $PROD_DB_HOST:5432"
else
    log_error "Falha na conexão com banco de dados: $PROD_DB_HOST:5432"
    exit 1
fi

# 4. Verificar variáveis de ambiente críticas
echo
log_info "4. Verificando variáveis de ambiente..."

required_vars=(
    "SEFAZ_CNPJ"
    "SEFAZ_RAZAO_SOCIAL"
    "SEFAZ_INSCRICAO_ESTADUAL"
    "SEFAZ_UF"
    "SEFAZ_ENDERECO"
    "SEFAZ_CEP"
    "AWS_CERTIFICATE_SECRET_NAME"
    "JWT_SECRET_KEY"
)

all_vars_ok=true
for var in "${required_vars[@]}"; do
    if [ -z "${!var}" ]; then
        log_error "Variável de ambiente não definida: $var"
        all_vars_ok=false
    else
        log_success "✓ $var"
    fi
done

if [ "$all_vars_ok" = false ]; then
    log_error "Variáveis de ambiente obrigatórias não definidas"
    exit 1
fi

# 5. Verificar configurações NFe
echo
log_info "5. Verificando configurações NFe..."

# Verificar se UseReal está true
if [ "${NFE_USE_REAL}" = "true" ]; then
    log_success "NFe configurado para produção (UseReal=true)"
else
    log_warning "NFe ainda em modo simulação (UseReal=${NFE_USE_REAL})"
fi

# Verificar ambiente SEFAZ
if [ "${SEFAZ_AMBIENTE}" = "1" ]; then
    log_success "SEFAZ configurado para produção (Ambiente=1)"
else
    log_warning "SEFAZ em ambiente de homologação (Ambiente=${SEFAZ_AMBIENTE})"
fi

# 6. Verificar espaço em disco
echo
log_info "6. Verificando recursos do sistema..."

available_space=$(df -h / | awk 'NR==2{print $4}')
log_info "Espaço disponível: $available_space"

# Verificar memória
available_memory=$(free -h | awk 'NR==2{print $7}')
log_info "Memória disponível: $available_memory"

# 7. Verificar backups
echo
log_info "7. Verificando estratégia de backup..."

if aws rds describe-db-instances --db-instance-identifier "${RDS_INSTANCE_ID}" --query 'DBInstances[0].BackupRetentionPeriod' --output text | grep -v "None" &>/dev/null; then
    log_success "Backups automáticos configurados no RDS"
else
    log_warning "Backups automáticos não configurados adequadamente"
fi

# 8. Verificar monitoramento
echo
log_info "8. Verificando monitoramento CloudWatch..."

if aws logs describe-log-groups --log-group-name-prefix "/aws/lambda/nfe-api" &>/dev/null; then
    log_success "Log groups CloudWatch configurados"
else
    log_warning "Log groups CloudWatch podem não estar configurados"
fi

# Resumo final
echo
echo "================================================"
log_info "🎯 RESUMO DA VALIDAÇÃO"
echo "================================================"

if [ "$all_vars_ok" = true ]; then
    log_success "✅ Sistema pronto para ativação de produção!"
    echo
    log_info "Próximos passos:"
    echo "1. Execute: bash scripts/backup-database.sh"
    echo "2. Execute: bash scripts/activate-production.sh"
    echo "3. Monitore: bash scripts/monitor-production.sh"
    echo
    exit 0
else
    log_error "❌ Sistema NÃO está pronto para produção"
    echo
    log_info "Corrija os erros acima antes de prosseguir"
    exit 1
fi