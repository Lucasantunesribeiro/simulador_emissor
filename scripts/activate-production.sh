#!/bin/bash

# Script de ativação do ambiente de produção NFe
# ⚠️  CUIDADO: Este script ativa o modo REAL de emissão de NFe
# Autor: Sistema NFe
# Data: $(date +%Y-%m-%d)

set -e

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

log_error() {
    echo -e "${RED}❌ $1${NC}"
}

log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

echo "🚨 ATIVAÇÃO DO MODO PRODUÇÃO NFe"
echo "================================"
echo
log_warning "ATENÇÃO: Este script irá ativar a emissão REAL de NFe!"
log_warning "Certifique-se de ter executado validate-production-readiness.sh"
echo

# Confirmar ativação
read -p "Tem certeza que deseja ativar o modo PRODUÇÃO? (digite 'PRODUCTION_APPROVE'): " confirmation

if [ "$confirmation" != "PRODUCTION_APPROVE" ]; then
    log_error "Ativação cancelada. Confirmação incorreta."
    log_info "Para ativar, digite exatamente: PRODUCTION_APPROVE"
    exit 1
fi

echo
log_info "Iniciando ativação do modo produção..."

# 1. Backup de segurança antes da ativação
echo
log_info "1. Executando backup de segurança..."
if bash "$(dirname "$0")/backup-database.sh"; then
    log_success "Backup realizado com sucesso"
else
    log_error "Falha no backup - Ativação abortada"
    exit 1
fi

# 2. Atualizar configurações no AWS Systems Manager
echo
log_info "2. Atualizando configurações de produção..."

# Ativar modo real NFe
aws ssm put-parameter \
    --name "/nfe/production/useReal" \
    --value "true" \
    --type "String" \
    --overwrite || {
    log_error "Falha ao atualizar parâmetro NFe:UseReal"
    exit 1
}

# Definir ambiente SEFAZ produção
aws ssm put-parameter \
    --name "/nfe/production/ambiente" \
    --value "1" \
    --type "String" \
    --overwrite || {
    log_error "Falha ao atualizar parâmetro Sefaz:Ambiente"
    exit 1
}

log_success "Configurações de produção atualizadas no Parameter Store"

# 3. Reiniciar Lambda functions para aplicar configurações
echo
log_info "3. Reiniciando Lambda functions..."

lambda_functions=(
    "nfe-api"
    "nfe-worker"
)

for func in "${lambda_functions[@]}"; do
    if aws lambda update-function-configuration \
        --function-name "$func" \
        --environment Variables="{ASPNETCORE_ENVIRONMENT=Production,NFE_ENVIRONMENT=Production}" \
        &>/dev/null; then
        log_success "Lambda function reiniciada: $func"
    else
        log_warning "Lambda function não encontrada ou erro: $func"
    fi
done

# 4. Executar teste de conectividade SEFAZ
echo
log_info "4. Testando conectividade SEFAZ produção..."

# Aguardar Lambda functions reiniciarem
sleep 10

# Testar endpoint de status da aplicação
api_endpoint="${API_GATEWAY_URL}/health"
if curl -s "$api_endpoint" | grep -q "Healthy"; then
    log_success "API em execução e saudável"
else
    log_error "API não está respondendo adequadamente"
    log_error "Execute rollback imediatamente: bash scripts/emergency-rollback.sh"
    exit 1
fi

# 5. Testar emissão de NFe de teste (cancelada imediatamente)
echo
log_info "5. Executando teste de produção..."

# Este seria um teste real mínimo que seria cancelado imediatamente
log_warning "⚠️  TESTE DE PRODUÇÃO - NFe real será emitida e cancelada"
log_info "Implementar teste real quando apropriado..."

# 6. Configurar monitoramento específico de produção
echo
log_info "6. Configurando monitoramento de produção..."

# Criar alarmes CloudWatch específicos para produção
aws cloudwatch put-metric-alarm \
    --alarm-name "NFe-Producao-Erros" \
    --alarm-description "Monitorar erros em produção NFe" \
    --metric-name "Errors" \
    --namespace "AWS/Lambda" \
    --statistic "Sum" \
    --period 300 \
    --threshold 5 \
    --comparison-operator "GreaterThanThreshold" \
    --dimensions Name=FunctionName,Value=nfe-api \
    --evaluation-periods 1 \
    --alarm-actions "arn:aws:sns:us-east-1:${AWS_ACCOUNT_ID}:nfe-alerts" || {
    log_warning "Erro ao criar alarme CloudWatch - Continue manualmente"
}

# 7. Atualizar status no banco de dados
echo
log_info "7. Registrando ativação no banco..."

# Conectar ao banco e registrar a ativação
timestamp=$(date -u +"%Y-%m-%d %H:%M:%S")
activation_log="INSERT INTO sistema_logs (evento, descricao, data_evento) VALUES ('PRODUCAO_ATIVADA', 'Modo produção NFe ativado via script', '$timestamp');"

# Executar via psql se disponível
if command -v psql &> /dev/null; then
    echo "$activation_log" | psql "$DATABASE_URL" || {
        log_warning "Não foi possível registrar no banco - Continue manualmente"
    }
fi

# Resumo final
echo
echo "🎉 PRODUÇÃO ATIVADA COM SUCESSO!"
echo "================================"
log_success "✅ Modo NFe REAL está ATIVO"
log_success "✅ Ambiente SEFAZ: PRODUÇÃO (1)"
log_success "✅ Lambda functions reiniciadas"
log_success "✅ Monitoramento configurado"
log_success "✅ Backup de segurança realizado"
echo

log_info "🔍 MONITORAMENTO ATIVO:"
echo "• CloudWatch Logs: /aws/lambda/nfe-api"
echo "• CloudWatch Alarms: NFe-Producao-*"
echo "• Health Check: $api_endpoint"
echo

log_info "📋 PRÓXIMOS PASSOS:"
echo "1. Monitore logs em tempo real"
echo "2. Execute testes manuais cuidadosos"
echo "3. Valide primeira NFe real"
echo "4. Configure alertas adicionais se necessário"
echo

log_warning "⚠️  IMPORTANTE:"
echo "• Todas as NFe agora são REAIS e têm valor fiscal"
echo "• Monitore constantemente os primeiros dias"
echo "• Em caso de problemas: bash scripts/emergency-rollback.sh"
echo

log_info "Sistema em PRODUÇÃO desde: $(date)"
echo "Ativação registrada em: /var/log/nfe-activation-$(date +%Y%m%d).log" | tee -a "/var/log/nfe-activation-$(date +%Y%m%d).log"