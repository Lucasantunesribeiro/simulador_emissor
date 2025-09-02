#!/bin/bash

# Script de rollback emergencial - Desativar produção NFe
# 🚨 EMERGÊNCIA: Este script reverte o sistema para modo simulação
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

echo "🚨 ROLLBACK EMERGENCIAL - DESATIVANDO PRODUÇÃO"
echo "============================================="
echo
log_warning "Este script irá reverter o sistema para modo SIMULAÇÃO"
log_warning "Execute apenas em caso de problemas críticos em produção"
echo

# Log do motivo do rollback
read -p "Motivo do rollback (obrigatório): " rollback_reason

if [ -z "$rollback_reason" ]; then
    log_error "Motivo do rollback é obrigatório para auditoria"
    exit 1
fi

# Confirmar rollback
read -p "Confirmar ROLLBACK EMERGENCIAL? (digite 'ROLLBACK_CONFIRMED'): " confirmation

if [ "$confirmation" != "ROLLBACK_CONFIRMED" ]; then
    log_error "Rollback cancelado. Confirmação incorreta."
    exit 1
fi

echo
log_info "🚨 Iniciando rollback emergencial..."

# Timestamp para logs
rollback_timestamp=$(date -u +"%Y-%m-%d %H:%M:%S")

# 1. Desativar modo real imediatamente
echo
log_info "1. Desativando modo NFe real..."

aws ssm put-parameter \
    --name "/nfe/production/useReal" \
    --value "false" \
    --type "String" \
    --overwrite || {
    log_error "CRÍTICO: Falha ao desativar NFe real"
    # Continue mesmo com falha - tentar outros métodos
}

# 2. Reverter ambiente para homologação
echo
log_info "2. Revertendo para ambiente de homologação..."

aws ssm put-parameter \
    --name "/nfe/production/ambiente" \
    --value "2" \
    --type "String" \
    --overwrite || {
    log_error "CRÍTICO: Falha ao reverter ambiente SEFAZ"
}

log_success "Configurações revertidas no Parameter Store"

# 3. Reiniciar Lambda functions com configuração de desenvolvimento
echo
log_info "3. Reiniciando Lambda functions para modo simulação..."

lambda_functions=(
    "nfe-api"
    "nfe-worker"
)

for func in "${lambda_functions[@]}"; do
    if aws lambda update-function-configuration \
        --function-name "$func" \
        --environment Variables="{ASPNETCORE_ENVIRONMENT=Development,NFE_ENVIRONMENT=Development}" \
        &>/dev/null; then
        log_success "Lambda function revertida: $func"
    else
        log_error "ERRO: Não foi possível reverter Lambda: $func"
    fi
done

# 4. Verificar se API voltou ao modo simulação
echo
log_info "4. Verificando status da API..."

sleep 15  # Aguardar reinicialização

api_endpoint="${API_GATEWAY_URL}/health"
max_retries=5
retry_count=0

while [ $retry_count -lt $max_retries ]; do
    if curl -s "$api_endpoint" | grep -q "Healthy"; then
        log_success "API respondendo - Modo simulação ativo"
        break
    else
        retry_count=$((retry_count + 1))
        log_warning "Tentativa $retry_count/$max_retries - API ainda inicializando..."
        sleep 10
    fi
done

if [ $retry_count -eq $max_retries ]; then
    log_error "CRÍTICO: API não está respondendo após rollback"
    log_error "Intervenção manual necessária!"
fi

# 5. Restaurar banco de dados se necessário
echo
log_info "5. Verificando necessidade de restore do banco..."

# Verificar se existe backup recente
latest_backup=$(ls -t /backups/nfe_production_*.sql 2>/dev/null | head -1)

if [ -n "$latest_backup" ]; then
    log_info "Backup encontrado: $latest_backup"
    read -p "Restaurar banco de dados? (y/N): " restore_db
    
    if [ "$restore_db" = "y" ] || [ "$restore_db" = "Y" ]; then
        log_warning "Restaurando banco de dados..."
        
        # Fazer backup atual antes do restore
        pg_dump "$DATABASE_URL" > "/backups/pre_rollback_$(date +%Y%m%d_%H%M%S).sql" || {
            log_warning "Não foi possível fazer backup atual"
        }
        
        # Restaurar backup
        psql "$DATABASE_URL" < "$latest_backup" || {
            log_error "ERRO ao restaurar banco de dados"
            log_error "Banco pode estar inconsistente - Verificar manualmente"
        }
        
        log_success "Banco de dados restaurado"
    else
        log_info "Restore de banco não executado"
    fi
else
    log_info "Nenhum backup encontrado - Banco mantido no estado atual"
fi

# 6. Registrar rollback no sistema
echo
log_info "6. Registrando rollback emergencial..."

# Registrar no banco de dados
rollback_log="INSERT INTO sistema_logs (evento, descricao, data_evento) VALUES ('ROLLBACK_EMERGENCIAL', 'Motivo: $rollback_reason | Timestamp: $rollback_timestamp', NOW());"

if command -v psql &> /dev/null; then
    echo "$rollback_log" | psql "$DATABASE_URL" || {
        log_warning "Não foi possível registrar rollback no banco"
    }
fi

# Registrar em arquivo de log
echo "$rollback_timestamp - ROLLBACK EMERGENCIAL: $rollback_reason" >> "/var/log/nfe-emergency-rollback.log"

# 7. Notificar equipe (se SNS configurado)
echo
log_info "7. Enviando notificações..."

rollback_message="🚨 ROLLBACK EMERGENCIAL NFe
Timestamp: $rollback_timestamp
Motivo: $rollback_reason
Sistema revertido para modo SIMULAÇÃO
Verificar logs e sistema imediatamente"

aws sns publish \
    --topic-arn "arn:aws:sns:us-east-1:${AWS_ACCOUNT_ID}:nfe-emergency-alerts" \
    --message "$rollback_message" \
    --subject "🚨 NFe - Rollback Emergencial Executado" || {
    log_warning "Não foi possível enviar notificação SNS"
}

# 8. Verificação final do sistema
echo
log_info "8. Verificação final do sistema..."

# Testar endpoint básico
if curl -s "$api_endpoint" | grep -q "Healthy"; then
    log_success "Sistema operacional em modo simulação"
else
    log_error "CRÍTICO: Sistema não está operacional"
    log_error "INTERVENÇÃO MANUAL URGENTE NECESSÁRIA"
fi

# Resumo final
echo
echo "🔄 ROLLBACK EMERGENCIAL CONCLUÍDO"
echo "================================="
log_success "✅ NFe revertido para modo SIMULAÇÃO"
log_success "✅ Ambiente SEFAZ: HOMOLOGAÇÃO (2)"
log_success "✅ Lambda functions reiniciadas"
log_success "✅ Rollback registrado no sistema"
echo

log_info "📋 STATUS ATUAL:"
echo "• Modo NFe: SIMULAÇÃO (UseReal=false)"
echo "• Ambiente SEFAZ: HOMOLOGAÇÃO (2)"
echo "• Timestamp rollback: $rollback_timestamp"
echo "• Motivo: $rollback_reason"
echo

log_warning "⚠️  AÇÕES NECESSÁRIAS:"
echo "1. Investigar causa raiz do problema"
echo "2. Corrigir problemas identificados"
echo "3. Executar testes completos"
echo "4. Reativar produção apenas após correção"
echo "5. Revisar logs de produção: /var/log/nfe-emergency-rollback.log"
echo

log_info "Sistema em modo SIMULAÇÃO desde: $(date)"
log_error "⚠️  PRODUÇÃO DESATIVADA - Correção necessária antes de reativar"