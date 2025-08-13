# Sistema de Emissão de NF-e (.NET 9)

## Visão Geral

Sistema completo para emissão automatizada de Nota Fiscal Eletrônica (NF-e) desenvolvido com .NET 9, seguindo arquitetura limpa e pronto para produção com Docker Compose.

## ⚠️ **IMPORTANTE: MODO SIMULAÇÃO**

> **Este sistema opera em modo de simulação por padrão e NÃO emite NFes reais.**
> 
> - Todas as operações são simuladas para fins de desenvolvimento e testes
> - Nenhuma comunicação real é feita com a SEFAZ
> - Para uso em produção, são necessárias configurações adicionais (veja seção abaixo)

## Como Rodar

### Pré-requisitos
- .NET 9 SDK
- Docker e Docker Compose (recomendado)

### Execução com Docker Compose (Recomendado)
```bash
# Subir todos os serviços (API, Worker e PostgreSQL)
docker-compose up -d

# Verificar logs
docker-compose logs -f

# Parar serviços
docker-compose down
```

A API estará disponível em: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- Health Check: `http://localhost:5000/health`

### Execução Manual (Desenvolvimento)
```bash
# Restaurar dependências
dotnet restore

# Build do projeto
dotnet build

# Executar API
dotnet run --project NFe.API

# Executar Worker (em outro terminal)
dotnet run --project NFe.Worker
```

## Arquitetura

O projeto segue os princípios de Clean Architecture com separação clara de responsabilidades:

- **NFe.API**: Camada de apresentação (REST API, controllers, health checks)
- **NFe.Core**: Camada de domínio (entidades, interfaces, regras de negócio)
- **NFe.Infrastructure**: Camada de infraestrutura (repositórios, integração externa)
- **NFe.Worker**: Serviço de background para processamento assíncrono

## CI/CD

O projeto inclui configuração básica para CI/CD:
- Dockerfiles otimizados para cada serviço
- Docker Compose para orquestração local
- Estrutura preparada para pipelines de deploy

## Configuração para Produção

Para usar este sistema em produção com emissão real de NFes, são necessárias as seguintes alterações:

### 1. Configuração SEFAZ
- Alterar URLs dos endpoints para os ambientes reais da SEFAZ
- Configurar certificados digitais válidos
- Implementar autenticação real com a SEFAZ

### 2. Assinatura Digital
- Integrar com Azure Key Vault ou HSM
- Configurar certificados A1/A3 válidos
- Implementar assinatura digital real dos XMLs

### 3. Banco de Dados
- Configurar banco de dados persistente (PostgreSQL em produção)
- Executar migrações do Entity Framework
- Configurar backup e recuperação

### 4. Monitoramento
- Configurar logs estruturados
- Implementar métricas de negócio
- Configurar alertas para falhas

### 5. Segurança
- Implementar autenticação e autorização
- Configurar HTTPS obrigatório
- Validar e sanitizar todas as entradas

## Tecnologias Utilizadas

- **.NET 9**: Framework principal
- **ASP.NET Core**: API REST
- **Entity Framework Core**: ORM para PostgreSQL
- **PostgreSQL**: Banco de dados
- **Docker**: Containerização
- **Swagger/OpenAPI**: Documentação da API
- **Health Checks**: Monitoramento de saúde

## Licença

Este projeto é fornecido como exemplo educacional. Para uso comercial, certifique-se de cumprir todas as regulamentações fiscais brasileiras.