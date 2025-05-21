# Emissão Automatizada de NF-e (.NET 9)

## Visão Geral

Projeto completo para emissão automatizada de Nota Fiscal Eletrônica (NF-e) com .NET 9, arquitetura em camadas, testes automatizados, health check, pronto para CI/CD, Docker e cache distribuído.

## Estrutura de Pastas

- **NFe.API**: API REST (controllers, endpoints, health check, swagger)
- **NFe.Core**: Domínio (entidades, interfaces, serviços de negócio)
- **NFe.Infrastructure**: Implementações (repositórios, integração SEFAZ, assinatura, validação XML)
- **NFe.Worker**: Serviço background para processar vendas pendentes
- **NFe.Tests**: Testes unitários (xUnit)

## Como Rodar

1. **Requisitos:** .NET 9 SDK, Docker (opcional para orquestração)
2. **Build:**
   ```bash
   dotnet build
   ```
3. **Testes:**
   ```bash
   dotnet test NFe.Tests/NFe.Tests.csproj
   ```
4. **Rodar API:**
   ```bash
   dotnet run --project NFe.API/NFe.API.csproj
   ```
5. **Rodar com Docker Compose:**
   ```bash
   docker-compose up -d
   ```

## Endpoints Principais

- **POST /api/v1/vendas**: Cria venda
- **GET /api/v1/vendas/{id}**: Detalhe da venda
- **GET /api/v1/vendas/{id}/status**: Status processamento
- **GET /api/v1/protocolos/{id}**: Detalhe protocolo
- **GET /api/v1/protocolos/chave/{chaveAcesso}**: Busca protocolo por chave
- **GET /health**: Health check
- **GET /swagger**: Documentação interativa

## CI/CD

- Pipeline GitHub Actions já configurado para build, test e deploy.
- Dockerfile e docker-compose prontos para produção e desenvolvimento.

## Simulação vs Produção Real

> **Este projeto está em modo de simulação por padrão.**

- **SefazClient**: Retorna respostas simuladas se a flag `_simulacao` for `true`. Para produção, troque para `false` e ajuste as URLs/endpoints reais da SEFAZ.
- **Assinador**: Simula assinatura digital. Para produção, implemente a integração real com Azure Key Vault ou outro provedor de certificado.
- **Repositórios**: Usam listas em memória. Para produção, troque por implementação com banco de dados (ex: Entity Framework Core, Dapper).

### Como alternar para produção real

1. **SefazClient**
   - Altere `private readonly bool _simulacao = true;` para `false` em `SefazClient.cs`.
   - Ajuste as URLs dos endpoints SEFAZ para os reais.
2. **Assinador**
   - Implemente o método `ObterCertificadoDoKeyVault` para buscar o certificado real.
3. **Repositórios**
   - Implemente repositórios persistentes (ex: EF Core, Dapper) e registre no DI.
4. **Testes**
   - Adapte/mantenha mocks para ambiente de testes.

> O restante do código já está pronto para produção, bastando trocar essas camadas.

