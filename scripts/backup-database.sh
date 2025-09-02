#!/bin/bash

# Script de backup do banco de dados NFe
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

echo "💾 BACKUP DO BANCO DE DADOS NFe"
echo "==============================="

# Configurações
BACKUP_DIR="/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILENAME="nfe_production_${TIMESTAMP}.sql"
BACKUP_PATH="${BACKUP_DIR}/${BACKUP_FILENAME}"

# Verificar se diretório de backup existe
if [ ! -d "$BACKUP_DIR" ]; then
    log_info "Criando diretório de backup: $BACKUP_DIR"
    mkdir -p "$BACKUP_DIR" || {
        log_error "Não foi possível criar diretório de backup"
        exit 1
    }
fi

# Verificar variáveis de ambiente necessárias
if [ -z "$DATABASE_URL" ]; then
    log_error "DATABASE_URL não definida"
    exit 1
fi

# 1. Backup completo do banco
echo
log_info "1. Executando backup completo..."

# Fazer dump do banco
pg_dump "$DATABASE_URL" > "$BACKUP_PATH" || {
    log_error "Falha ao executar pg_dump"
    exit 1
}

log_success "Backup criado: $BACKUP_PATH"

# Verificar se backup foi criado corretamente
if [ -f "$BACKUP_PATH" ] && [ -s "$BACKUP_PATH" ]; then
    backup_size=$(du -h "$BACKUP_PATH" | cut -f1)
    log_success "Backup válido - Tamanho: $backup_size"
else
    log_error "Backup inválido ou vazio"
    exit 1
fi

# 2. Compactar backup
echo
log_info "2. Compactando backup..."

gzip "$BACKUP_PATH" || {
    log_warning "Não foi possível compactar o backup"
}

COMPRESSED_BACKUP="${BACKUP_PATH}.gz"
if [ -f "$COMPRESSED_BACKUP" ]; then
    compressed_size=$(du -h "$COMPRESSED_BACKUP" | cut -f1)
    log_success "Backup compactado: $compressed_size"
    BACKUP_PATH="$COMPRESSED_BACKUP"
fi

# 3. Upload para S3 (se configurado)
echo
log_info "3. Upload para S3..."

if [ -n "$AWS_BACKUP_BUCKET" ]; then
    s3_path="s3://${AWS_BACKUP_BUCKET}/nfe-database-backups/${BACKUP_FILENAME}.gz"
    
    aws s3 cp "$BACKUP_PATH" "$s3_path" || {
        log_warning "Não foi possível fazer upload para S3"
        log_info "Backup permanece local em: $BACKUP_PATH"
    }
    
    if aws s3 ls "$s3_path" > /dev/null 2>&1; then
        log_success "Backup enviado para S3: $s3_path"
    fi
else
    log_info "AWS_BACKUP_BUCKET não configurado - Backup apenas local"
fi

# 4. Limpeza de backups antigos (manter últimos 7 dias localmente)
echo
log_info "4. Limpeza de backups antigos..."

find "$BACKUP_DIR" -name "nfe_production_*.sql*" -type f -mtime +7 -delete 2>/dev/null || {
    log_info "Nenhum backup antigo para remover"
}

log_success "Limpeza de backups antigos concluída"

# 5. Verificação de integridade do backup
echo
log_info "5. Verificação de integridade..."

# Testar se o backup pode ser lido
if [ "${BACKUP_PATH}" != "${BACKUP_PATH%.gz}" ]; then
    # Arquivo compactado
    if gzip -t "$BACKUP_PATH"; then
        log_success "Integridade do arquivo compactado: OK"
    else
        log_error "Arquivo compactado corrompido"
        exit 1
    fi
else
    # Arquivo não compactado
    if head -n 10 "$BACKUP_PATH" | grep -q "PostgreSQL database dump"; then
        log_success "Integridade do backup: OK"
    else
        log_error "Backup não parece ser um dump PostgreSQL válido"
        exit 1
    fi
fi

# 6. Registrar backup no sistema
echo
log_info "6. Registrando backup no sistema..."

backup_log="INSERT INTO sistema_logs (evento, descricao, data_evento) VALUES ('BACKUP_CRIADO', 'Backup: ${BACKUP_FILENAME} | Tamanho: ${compressed_size:-$backup_size}', NOW());"

if command -v psql &> /dev/null; then
    echo "$backup_log" | psql "$DATABASE_URL" || {
        log_warning "Não foi possível registrar backup no sistema"
    }
fi

# Relatório final
echo
echo "📊 RELATÓRIO DE BACKUP"
echo "====================="
log_success "✅ Backup concluído com sucesso"
echo
log_info "Detalhes:"
echo "• Nome: $BACKUP_FILENAME"
echo "• Caminho: $BACKUP_PATH"
echo "• Tamanho: ${compressed_size:-$backup_size}"
echo "• Timestamp: $TIMESTAMP"
if [ -n "$AWS_BACKUP_BUCKET" ]; then
    echo "• S3: s3://${AWS_BACKUP_BUCKET}/nfe-database-backups/${BACKUP_FILENAME}.gz"
fi
echo

log_info "Para restaurar:"
if [ "${BACKUP_PATH}" != "${BACKUP_PATH%.gz}" ]; then
    echo "gunzip -c $BACKUP_PATH | psql [DATABASE_URL]"
else
    echo "psql [DATABASE_URL] < $BACKUP_PATH"
fi
echo

log_success "Backup disponível para uso em ativação/rollback de produção"