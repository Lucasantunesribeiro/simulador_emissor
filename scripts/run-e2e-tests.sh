#!/bin/bash

# Script para executar testes E2E completos no ambiente de homologação
# Projeto NFe - Sistema de Emissão de Nota Fiscal Eletrônica

set -e

echo "=================================="
echo "🚀 INICIANDO TESTES E2E COMPLETOS"
echo "=================================="

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Função para logging
log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

log_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

log_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Verificar se estamos na raiz do projeto
if [ ! -f "NFe.sln" ]; then
    log_error "Execute este script na raiz do projeto NFe"
    exit 1
fi

# Verificar Docker
if ! docker --version > /dev/null 2>&1; then
    log_error "Docker não está instalado ou não está em execução"
    exit 1
fi

# Verificar .NET 9
if ! dotnet --version | grep -q "9."; then
    log_error ".NET 9 não está instalado"
    exit 1
fi

# Verificar Node.js
if ! node --version > /dev/null 2>&1; then
    log_error "Node.js não está instalado"
    exit 1
fi

log_info "Iniciando ambiente de teste..."

# 1. Subir ambiente de teste com Docker Compose
log_info "Subindo containers de teste..."
docker-compose -f docker-compose.test.yml up -d

# Aguardar serviços ficarem prontos
log_info "Aguardando serviços ficarem prontos..."
sleep 30

# Verificar se PostgreSQL está pronto
until docker-compose -f docker-compose.test.yml exec postgres-test pg_isready -U nfeadmin > /dev/null 2>&1; do
    log_info "Aguardando PostgreSQL..."
    sleep 5
done
log_success "PostgreSQL pronto"

# Verificar se LocalStack está pronto
until curl -f http://localhost:4566/health > /dev/null 2>&1; do
    log_info "Aguardando LocalStack..."
    sleep 5
done
log_success "LocalStack pronto"

# 2. Executar testes backend
log_info "🧪 EXECUTANDO TESTES BACKEND E2E"
echo "================================"

cd NFe.Tests.E2E

log_info "Restaurando pacotes..."
dotnet restore

log_info "Executando testes de fluxo completo..."
dotnet test --filter "Category=FluxoCompleto" --logger "console;verbosity=detailed" || true

log_info "Executando testes de integração SEFAZ..."
dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed" || true

log_info "Executando testes de Worker SQS..."
dotnet test --filter "Category=Worker" --logger "console;verbosity=detailed" || true

log_info "Executando testes de performance..."
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed" || true

log_info "Executando testes de segurança..."
dotnet test --filter "Category=Security" --logger "console;verbosity=detailed" || true

log_info "Executando todos os testes E2E..."
dotnet test --logger "console;verbosity=detailed" --logger "junit;LogFilePath=../test-results/backend-results.xml"

cd ..

log_success "Testes backend concluídos"

# 3. Executar testes frontend
log_info "🎭 EXECUTANDO TESTES FRONTEND E2E"
echo "================================="

cd NFe.WebApp

log_info "Instalando dependências..."
npm ci

log_info "Instalando Playwright..."
npx playwright install --with-deps

log_info "Executando testes de autenticação..."
npx playwright test auth-flow --reporter=line || true

log_info "Executando testes de vendas..."
npx playwright test vendas-flow --reporter=line || true

log_info "Executando testes de dashboard..."
npx playwright test dashboard --reporter=line || true

log_info "Executando todos os testes Playwright..."
npx playwright test --reporter=html,line

cd ..

log_success "Testes frontend concluídos"

# 4. Gerar relatórios
log_info "📊 GERANDO RELATÓRIOS"
echo "===================="

mkdir -p test-results

# Copiar relatórios Playwright
if [ -d "NFe.WebApp/playwright-report" ]; then
    cp -r NFe.WebApp/playwright-report test-results/
    log_success "Relatório Playwright copiado"
fi

# Gerar relatório de cobertura se disponível
if [ -f "NFe.Tests.E2E/TestResults/*/coverage.cobertura.xml" ]; then
    log_info "Gerando relatório de cobertura..."
    dotnet tool install --global dotnet-reportgenerator-globaltool || true
    reportgenerator -reports:NFe.Tests.E2E/TestResults/*/coverage.cobertura.xml -targetdir:test-results/coverage-report || true
    log_success "Relatório de cobertura gerado"
fi

# 5. Coleta de logs
log_info "📋 COLETANDO LOGS"
echo "================="

mkdir -p test-results/logs

# Logs dos containers
docker-compose -f docker-compose.test.yml logs postgres-test > test-results/logs/postgres.log || true
docker-compose -f docker-compose.test.yml logs redis-test > test-results/logs/redis.log || true
docker-compose -f docker-compose.test.yml logs localstack > test-results/logs/localstack.log || true
docker-compose -f docker-compose.test.yml logs api-test > test-results/logs/api.log || true
docker-compose -f docker-compose.test.yml logs worker-test > test-results/logs/worker.log || true

log_success "Logs coletados"

# 6. Limpeza
log_info "🧹 LIMPEZA DO AMBIENTE"
echo "====================="

log_info "Parando containers de teste..."
docker-compose -f docker-compose.test.yml down -v

log_info "Removendo volumes não utilizados..."
docker volume prune -f > /dev/null 2>&1 || true

log_success "Limpeza concluída"

# 7. Resumo final
echo ""
echo "=================================="
echo "📋 RESUMO DOS TESTES E2E"
echo "=================================="

if [ -f "test-results/backend-results.xml" ]; then
    BACKEND_TESTS=$(grep -o 'tests="[0-9]*"' test-results/backend-results.xml | grep -o '[0-9]*' || echo "0")
    BACKEND_FAILURES=$(grep -o 'failures="[0-9]*"' test-results/backend-results.xml | grep -o '[0-9]*' || echo "0")
    echo "Backend: $BACKEND_TESTS testes, $BACKEND_FAILURES falhas"
fi

if [ -d "NFe.WebApp/test-results" ]; then
    FRONTEND_REPORT=$(find NFe.WebApp/test-results -name "*.json" -exec cat {} \; 2>/dev/null | grep -o '"passed":[0-9]*' | head -1 | grep -o '[0-9]*' || echo "0")
    echo "Frontend: Relatório disponível em test-results/playwright-report/"
fi

echo ""
log_info "Relatórios disponíveis em: ./test-results/"
log_info "Logs dos containers em: ./test-results/logs/"

if [ -f "test-results/playwright-report/index.html" ]; then
    log_info "Relatório Playwright: file://$(pwd)/test-results/playwright-report/index.html"
fi

if [ -f "test-results/coverage-report/index.html" ]; then
    log_info "Relatório de cobertura: file://$(pwd)/test-results/coverage-report/index.html"
fi

echo ""
echo "=================================="
log_success "🎉 TESTES E2E CONCLUÍDOS"
echo "=================================="

echo ""
log_warning "PRÓXIMOS PASSOS:"
echo "1. Revisar relatórios de teste"
echo "2. Verificar cobertura de código"
echo "3. Analisar logs para possíveis melhorias"
echo "4. Configurar certificado A1 real para homologação"
echo "5. Executar testes com dados reais da SEFAZ"