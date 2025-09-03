#!/bin/bash

# Script para validar a configuração dos testes E2E
# Verifica se todos os componentes estão corretos antes da execução

set -e

echo "========================================="
echo "🔍 VALIDANDO CONFIGURAÇÃO TESTES E2E"
echo "========================================="

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() { echo -e "${BLUE}ℹ️  $1${NC}"; }
log_success() { echo -e "${GREEN}✅ $1${NC}"; }
log_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
log_error() { echo -e "${RED}❌ $1${NC}"; }

ERRORS=0

# 1. Verificar estrutura do projeto
log_info "Verificando estrutura do projeto..."

check_file() {
    if [ -f "$1" ]; then
        log_success "Arquivo encontrado: $1"
    else
        log_error "Arquivo não encontrado: $1"
        ((ERRORS++))
    fi
}

check_dir() {
    if [ -d "$1" ]; then
        log_success "Diretório encontrado: $1"
    else
        log_error "Diretório não encontrado: $1"
        ((ERRORS++))
    fi
}

# Arquivos principais
check_file "NFe.sln"
check_file "docker-compose.test.yml"
check_file "TESTING.md"
check_file "HOMOLOGATION.md"

# Configuração de homologação
check_file "NFe.API/appsettings.Homologation.json"

# Projeto de testes E2E
check_dir "NFe.Tests.E2E"
check_file "NFe.Tests.E2E/NFe.Tests.E2E.csproj"
check_file "NFe.Tests.E2E/TestFixture.cs"
check_file "NFe.Tests.E2E/TestDataFactory.cs"
check_file "NFe.Tests.E2E/FluxoCompletoTests.cs"
check_file "NFe.Tests.E2E/SefazIntegrationTests.cs"
check_file "NFe.Tests.E2E/WorkerSqsTests.cs"
check_file "NFe.Tests.E2E/PerformanceTests.cs"
check_file "NFe.Tests.E2E/SecurityTests.cs"

# Testes Playwright
check_dir "NFe.WebApp/tests"
check_dir "NFe.WebApp/tests/e2e"
check_file "NFe.WebApp/playwright.config.ts"
check_file "NFe.WebApp/tests/e2e/auth-flow.spec.ts"
check_file "NFe.WebApp/tests/e2e/vendas-flow.spec.ts"
check_file "NFe.WebApp/tests/e2e/dashboard.spec.ts"

# Scripts
check_dir "scripts"
check_file "scripts/run-e2e-tests.sh"

# 2. Verificar dependências
log_info "Verificando dependências..."

# .NET
if dotnet --version | grep -q "9."; then
    log_success ".NET 9 instalado: $(dotnet --version)"
else
    log_error ".NET 9 não encontrado"
    ((ERRORS++))
fi

# Docker
if docker --version > /dev/null 2>&1; then
    log_success "Docker instalado: $(docker --version | head -1)"
else
    log_error "Docker não encontrado"
    ((ERRORS++))
fi

# Docker Compose
if docker-compose --version > /dev/null 2>&1; then
    log_success "Docker Compose instalado: $(docker-compose --version)"
else
    log_error "Docker Compose não encontrado"
    ((ERRORS++))
fi

# Node.js
if node --version > /dev/null 2>&1; then
    log_success "Node.js instalado: $(node --version)"
else
    log_error "Node.js não encontrado"
    ((ERRORS++))
fi

# NPM
if npm --version > /dev/null 2>&1; then
    log_success "NPM instalado: $(npm --version)"
else
    log_error "NPM não encontrado"
    ((ERRORS++))
fi

# 3. Verificar configurações específicas
log_info "Verificando configurações..."

# Verificar se appsettings.Homologation.json tem as configurações corretas
if [ -f "NFe.API/appsettings.Homologation.json" ]; then
    if grep -q '"Ambiente": 2' NFe.API/appsettings.Homologation.json; then
        log_success "Ambiente de homologação configurado (2)"
    else
        log_warning "Ambiente de homologação pode não estar configurado corretamente"
    fi
    
    if grep -q '"UseReal": true' NFe.API/appsettings.Homologation.json; then
        log_success "UseReal configurado como true"
    else
        log_warning "UseReal pode não estar configurado para homologação"
    fi
fi

# Verificar se playwright está configurado no package.json
if [ -f "NFe.WebApp/package.json" ]; then
    if grep -q "@playwright/test" NFe.WebApp/package.json; then
        log_success "Playwright configurado no package.json"
    else
        log_error "Playwright não encontrado no package.json"
        ((ERRORS++))
    fi
fi

# 4. Verificar referências de projeto
log_info "Verificando referências de projeto..."

if grep -q "NFe.Tests.E2E" NFe.sln; then
    log_success "Projeto NFe.Tests.E2E incluído na solution"
else
    log_error "Projeto NFe.Tests.E2E não encontrado na solution"
    ((ERRORS++))
fi

# 5. Testar build dos projetos
log_info "Testando build dos projetos..."

# Build da solution
if dotnet build NFe.sln --configuration Release > /dev/null 2>&1; then
    log_success "Build da solution bem-sucedido"
else
    log_warning "Erro no build da solution - execute 'dotnet build' para detalhes"
    ((ERRORS++))
fi

# Verificar se o projeto de testes E2E compila
if dotnet build NFe.Tests.E2E/NFe.Tests.E2E.csproj > /dev/null 2>&1; then
    log_success "Build do projeto E2E bem-sucedido"
else
    log_warning "Erro no build do projeto E2E"
    ((ERRORS++))
fi

# 6. Verificar dependências do frontend
log_info "Verificando dependências do frontend..."

if [ -f "NFe.WebApp/package-lock.json" ]; then
    cd NFe.WebApp
    if npm ci > /dev/null 2>&1; then
        log_success "Dependências do frontend instaladas"
    else
        log_warning "Erro ao instalar dependências do frontend"
        ((ERRORS++))
    fi
    cd ..
fi

# 7. Verificar conectividade de rede (se necessário)
log_info "Verificando conectividade..."

# Verificar se consegue acessar ports necessários
if nc -z localhost 5432 2>/dev/null; then
    log_warning "Porta PostgreSQL (5432) em uso - pode interferir com testes"
fi

if nc -z localhost 4566 2>/dev/null; then
    log_warning "Porta LocalStack (4566) em uso - pode interferir com testes"
fi

# 8. Verificar permissões de scripts
log_info "Verificando permissões de scripts..."

if [ -x "scripts/run-e2e-tests.sh" ]; then
    log_success "Script run-e2e-tests.sh é executável"
else
    log_warning "Tornando script executável..."
    chmod +x scripts/run-e2e-tests.sh
    log_success "Script agora é executável"
fi

if [ -x "scripts/validate-e2e-setup.sh" ]; then
    log_success "Script validate-e2e-setup.sh é executável"
else
    chmod +x scripts/validate-e2e-setup.sh
fi

# 9. Resumo da validação
echo ""
echo "========================================="
echo "📋 RESUMO DA VALIDAÇÃO"
echo "========================================="

if [ $ERRORS -eq 0 ]; then
    log_success "🎉 Todas as validações passaram!"
    log_success "Sistema pronto para executar testes E2E"
    echo ""
    log_info "Para executar os testes, use:"
    echo "  ./scripts/run-e2e-tests.sh"
    echo ""
    log_info "Para executar apenas testes backend:"
    echo "  cd NFe.Tests.E2E && dotnet test"
    echo ""
    log_info "Para executar apenas testes frontend:"
    echo "  cd NFe.WebApp && npx playwright test"
else
    log_error "❌ Encontrados $ERRORS problemas"
    log_error "Corrija os problemas antes de executar os testes"
    echo ""
    log_info "Problemas comuns e soluções:"
    echo "- .NET 9 não instalado: https://dot.net"
    echo "- Docker não instalado: https://docker.com"
    echo "- Node.js não instalado: https://nodejs.org"
    echo "- Restaurar pacotes: dotnet restore"
    echo "- Instalar dependências: cd NFe.WebApp && npm install"
fi

echo ""
echo "========================================="

exit $ERRORS