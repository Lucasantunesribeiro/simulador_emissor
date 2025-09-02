# 🚨 ATIVAÇÃO DO MODO PRODUÇÃO NFe

> **ATENÇÃO CRÍTICA**: Este documento descreve o processo de ativação do modo REAL de emissão de NFe. Uma vez ativado, todas as NFe emitidas terão valor fiscal legal.

## 📋 Pré-requisitos OBRIGATÓRIOS

### 🔐 Certificados e Segurança
- [ ] Certificado A1 válido com mais de 90 dias para expiração
- [ ] Certificado testado em ambiente de homologação
- [ ] Certificado armazenado no AWS Secrets Manager
- [ ] Senhas e chaves JWT armazenadas no AWS Secrets Manager

### 🏢 Dados do Emitente
- [ ] CNPJ válido e ativo na Receita Federal
- [ ] Inscrição Estadual válida
- [ ] Endereço completo e atualizado
- [ ] Regime tributário definido corretamente

### 🌐 Conectividade SEFAZ
- [ ] Conectividade com SEFAZ produção validada
- [ ] URLs de produção configuradas
- [ ] Timeout e retry policies configurados
- [ ] Testes de conectividade executados

### 🗄️ Infraestrutura
- [ ] RDS em Multi-AZ configurado
- [ ] Backups automáticos habilitados (30 dias)
- [ ] Monitoramento CloudWatch ativo
- [ ] Alertas configurados no SNS
- [ ] Lambda functions em produção implantadas

## 🔧 Processo de Ativação

### Fase 1: Validação Pré-Ativação

1. **Execute o Script de Validação**:
   ```bash
   cd scripts
   chmod +x validate-production-readiness.sh
   ./validate-production-readiness.sh
   ```

2. **Revise Todos os Resultados**:
   - Certificado A1 acessível
   - SEFAZ produção conectável
   - Banco de dados operacional
   - Variáveis de ambiente configuradas

### Fase 2: Backup de Segurança

1. **Execute Backup Completo**:
   ```bash
   ./scripts/backup-database.sh
   ```

2. **Confirme Backup Válido**:
   - Verifique arquivo de backup criado
   - Confirme upload para S3 (se configurado)
   - Teste integridade do arquivo

### Fase 3: Ativação do Sistema

1. **Execute Script de Ativação**:
   ```bash
   ./scripts/activate-production.sh
   ```

2. **Confirmação Obrigatória**:
   ```
   Digite exatamente: PRODUCTION_APPROVE
   ```

3. **Aguarde Processo Completo**:
   - Atualização de parâmetros AWS
   - Reinicialização de Lambda functions
   - Configuração de monitoramento
   - Testes de conectividade

### Fase 4: Validação Pós-Ativação

1. **Verifique Status da API**:
   ```bash
   curl https://api.nfe.yourcompany.com/health
   ```

2. **Confirme Logs de Produção**:
   ```
   Procure por: "🚨 SEFAZ PRODUÇÃO ATIVADO"
   ```

3. **Execute Teste Controlado**:
   - Emitir NFe de teste mínima
   - Cancelar imediatamente
   - Verificar resposta SEFAZ

## 📊 Monitoramento Pós-Ativação

### CloudWatch Dashboards
- CPU/Memory das Lambda functions
- Latência das requisições
- Erros por minuto
- Conectividade SEFAZ

### Alarmes Críticos
- `NFe-Producao-Erros`: > 5 erros em 5 minutos
- `NFe-Producao-Latencia`: > 10s de latência
- `SEFAZ-Connectivity`: Falhas de conectividade

### Logs Essenciais
```bash
# CloudWatch Log Groups
/aws/lambda/nfe-api-production
/aws/lambda/nfe-worker-production

# Filtros importantes
"🚨 SEFAZ PRODUÇÃO ATIVADO"
"ERROR"
"EXCEPTION"
"NFe emitida"
```

## 🔄 Configurações Críticas Ativadas

### appsettings.Production.json
```json
{
  "NFe": {
    "UseReal": true
  },
  "Sefaz": {
    "Ambiente": 1,
    "UrlProducao": "https://nfe.fazenda.sp.gov.br/ws/..."
  }
}
```

### Variáveis de Ambiente
```bash
ASPNETCORE_ENVIRONMENT=Production
NFE_ENVIRONMENT=Production
NFE_USE_REAL=true
SEFAZ_AMBIENTE=1
```

### Service Configuration
```csharp
// ServiceCollectionExtensions.cs
if (useRealNFe && ambiente == 1) {
    services.AddScoped<INFeService, RealNFeService>();
    // Log crítico de produção ativo
}
```

## ⚠️ Cuidados Especiais

### Durante as Primeiras 24h
- Monitoramento 24/7 obrigatório
- Validação manual de cada NFe
- Backup incremental a cada 2 horas
- Alertas em tempo real ativos

### Indicadores de Problema
- Latência > 10 segundos
- Taxa de erro > 2%
- Falhas de conectividade SEFAZ
- Certificado próximo ao vencimento
- Espaço em disco < 20%

### Ações Imediatas em Caso de Problema
1. Execute rollback: `./scripts/emergency-rollback.sh`
2. Analise logs de erro
3. Comunique à equipe
4. Documente incidente
5. Corrija problema antes de reativar

## 🆘 Contatos de Emergência

### Equipe Técnica
- **Backend Lead**: [contato]
- **DevOps**: [contato]
- **Product Owner**: [contato]

### Fornecedores
- **Suporte SEFAZ**: 0800...
- **AWS Support**: [caso_premium]
- **Certificadora**: [contato]

## 📝 Registro de Ativações

| Data | Versão | Responsável | Status | Observações |
|------|--------|-------------|--------|-------------|
| YYYY-MM-DD | v1.0 | [Nome] | ✅ Sucesso | Primeira ativação |

## 🔐 Segurança e Auditoria

### Logs de Auditoria Obrigatórios
- Timestamp de ativação
- Usuário responsável
- Configurações alteradas
- Resultados dos testes

### Retenção de Dados
- Logs de produção: 90 dias
- Backups de banco: 1 ano
- Logs de auditoria: 5 anos

### Conformidade
- LGPD: Dados pessoais protegidos
- SPED: Logs fiscais mantidos
- SOX: Auditoria de alterações

## 📚 Documentos Relacionados

- [ROLLBACK_PROCEDURES.md](./ROLLBACK_PROCEDURES.md)
- [PRODUCTION_CHECKLIST.md](./PRODUCTION_CHECKLIST.md)
- [HOMOLOGATION.md](./HOMOLOGATION.md)
- [MONITORING_GUIDE.md](./MONITORING_GUIDE.md)

---

> **⚠️ IMPORTANTE**: Este documento deve ser atualizado após cada ativação de produção. Versão atual: 1.0 | Última atualização: 2025-01-20