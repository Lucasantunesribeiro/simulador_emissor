# 🔄 PROCEDIMENTOS DE ROLLBACK - NFe Produção

> **EMERGÊNCIA**: Este documento descreve os procedimentos para reverter o sistema NFe do modo produção para simulação em caso de problemas críticos.

## 🚨 Quando Executar Rollback

### Situações de Emergência IMEDIATA
- [ ] Taxa de erro > 10% por mais de 5 minutos
- [ ] Latência > 30 segundos constante
- [ ] Falha total de conectividade SEFAZ
- [ ] Certificado A1 expirado ou inválido
- [ ] Dados fiscais incorretos sendo emitidos
- [ ] Violação de segurança detectada

### Situações de Rollback PLANEJADO
- [ ] Problemas de performance persistentes
- [ ] Bugs críticos descobertos pós-produção
- [ ] Necessidade de manutenção emergencial
- [ ] Atualização urgente de certificado
- [ ] Mudanças regulatórias SEFAZ

## ⚡ Rollback Emergencial (< 5 minutos)

### Script Automático
```bash
cd /mnt/d/Programacao/emissao_nfe/scripts
./emergency-rollback.sh
```

### Confirmação Obrigatória
```
Digite exatamente: ROLLBACK_CONFIRMED
Motivo (obrigatório): [descrever problema]
```

### O que o Script Executa
1. **Desativa NFe Real** (UseReal=false)
2. **Reverte SEFAZ** para homologação (Ambiente=2)
3. **Reinicia Lambda Functions**
4. **Restaura configurações de desenvolvimento**
5. **Registra rollback no sistema**
6. **Envia alertas de emergência**

## 🛠️ Rollback Manual (Backup do Automático)

### Passo 1: Desativar NFe Real
```bash
# AWS Parameter Store
aws ssm put-parameter \
    --name "/nfe/production/useReal" \
    --value "false" \
    --overwrite

aws ssm put-parameter \
    --name "/nfe/production/ambiente" \
    --value "2" \
    --overwrite
```

### Passo 2: Reiniciar Lambda Functions
```bash
# API
aws lambda update-function-configuration \
    --function-name nfe-api \
    --environment Variables="{ASPNETCORE_ENVIRONMENT=Development}"

# Worker
aws lambda update-function-configuration \
    --function-name nfe-worker \
    --environment Variables="{ASPNETCORE_ENVIRONMENT=Development}"
```

### Passo 3: Verificar Reversão
```bash
# Testar endpoint
curl https://api.nfe.yourcompany.com/health

# Verificar logs
aws logs filter-log-events \
    --log-group-name "/aws/lambda/nfe-api" \
    --filter-pattern "DESENVOLVIMENTO"
```

## 🗄️ Restore de Banco de Dados

### Cenários para Restore
- Dados corrompidos durante produção
- NFe emitidas com dados incorretos
- Necessidade de voltar a estado anterior

### Localizar Backup Adequado
```bash
# Listar backups disponíveis
ls -la /backups/nfe_production_*.sql.gz

# Verificar backups no S3
aws s3 ls s3://nfe-production-backups/nfe-database-backups/
```

### Executar Restore
```bash
# Backup atual antes do restore
pg_dump $DATABASE_URL > /backups/pre_rollback_$(date +%Y%m%d_%H%M%S).sql

# Restore do backup escolhido
gunzip -c /backups/nfe_production_YYYYMMDD_HHMMSS.sql.gz | psql $DATABASE_URL
```

## 📊 Validação Pós-Rollback

### Checklist de Verificação
- [ ] API responde corretamente
- [ ] Modo simulação ativo (UseReal=false)
- [ ] Ambiente homologação (Ambiente=2)
- [ ] Logs indicam desenvolvimento/simulação
- [ ] Testes básicos funcionando
- [ ] Banco de dados consistente

### Comandos de Validação
```bash
# Status da API
curl -s https://api.nfe.yourcompany.com/health | jq

# Verificar configuração atual
aws ssm get-parameter --name "/nfe/production/useReal"
aws ssm get-parameter --name "/nfe/production/ambiente"

# Testar emissão em simulação
curl -X POST https://api.nfe.yourcompany.com/api/vendas/simular \
  -H "Content-Type: application/json" \
  -d '{"valor": 100.00, "descricao": "Teste rollback"}'
```

## 📝 Documentação do Rollback

### Informações Obrigatórias
```markdown
## Rollback Executado
- **Data/Hora**: YYYY-MM-DD HH:MM:SS UTC
- **Responsável**: [Nome]
- **Motivo**: [Descrição detalhada]
- **Duração**: XX minutos
- **Método**: Automático/Manual
- **Backup Usado**: [Nome do arquivo]

## Impacto
- **NFe Afetadas**: XX
- **Usuários Impactados**: XX
- **Downtime**: XX minutos

## Causa Raiz
[Análise detalhada do problema]

## Ações Corretivas
[O que será feito para evitar recorrência]
```

### Registro no Sistema
```sql
INSERT INTO sistema_logs (
    evento, 
    descricao, 
    data_evento,
    severidade
) VALUES (
    'ROLLBACK_EXECUTADO',
    'Motivo: [motivo] | Responsável: [nome] | Método: [automático/manual]',
    NOW(),
    'CRITICAL'
);
```

## 🔍 Análise Pós-Rollback

### Logs para Análise
```bash
# Logs de erro que levaram ao rollback
aws logs filter-log-events \
    --log-group-name "/aws/lambda/nfe-api" \
    --start-time $(date -d '1 hour ago' +%s)000 \
    --filter-pattern "ERROR"

# Métricas de performance
aws cloudwatch get-metric-statistics \
    --namespace "AWS/Lambda" \
    --metric-name "Duration" \
    --dimensions Name=FunctionName,Value=nfe-api \
    --start-time $(date -d '1 hour ago' -u +%Y-%m-%dT%H:%M:%S) \
    --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
    --period 300 \
    --statistics Average
```

### Relatório de Incidente
1. **Timeline detalhado** dos eventos
2. **Causa raiz** identificada
3. **Impacto** nos usuários
4. **Ações corretivas** implementadas
5. **Melhorias** para prevenção

## 🔄 Preparação para Reativação

### Antes de Reativar Produção
- [ ] Causa raiz corrigida
- [ ] Código corrigido e testado
- [ ] Testes de carga executados
- [ ] Monitoramento aprimorado
- [ ] Plano de rollback atualizado
- [ ] Equipe em standby

### Processo de Reativação
1. Aguardar estabilização (mínimo 2 horas)
2. Executar testes completos em homologação
3. Validar correções com equipe
4. Agendar nova ativação
5. Executar processo normal de ativação

## 📞 Contatos para Rollback

### Equipe de Resposta a Incidentes
| Função | Nome | Contato | Backup |
|--------|------|---------|---------|
| **Incident Commander** | [Nome] | [Tel] | [Nome] |
| **Backend Lead** | [Nome] | [Tel] | [Nome] |
| **DevOps Engineer** | [Nome] | [Tel] | [Nome] |
| **Product Owner** | [Nome] | [Tel] | [Nome] |

### Escalation Path
1. **Nível 1**: Desenvolvedor que detectou o problema
2. **Nível 2**: Backend Lead (após 15 minutos)
3. **Nível 3**: CTO (após 30 minutos)
4. **Nível 4**: CEO (após 1 hora ou impacto crítico)

## 🏥 Cenários de Rollback Específicos

### Certificado Expirado
```bash
# Verificar expiração
aws secretsmanager get-secret-value \
    --secret-id nfe-prod-certificate \
    --query 'SecretString' | \
    openssl x509 -noout -enddate

# Rollback imediato necessário
./scripts/emergency-rollback.sh
```

### Falha de Conectividade SEFAZ
```bash
# Testar conectividade
curl -I --connect-timeout 10 \
    https://nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx

# Se falhar > 5 minutos: rollback
./scripts/emergency-rollback.sh
```

### Performance Degradada
```bash
# Monitorar latência
aws cloudwatch get-metric-statistics \
    --namespace "AWS/Lambda" \
    --metric-name "Duration" \
    --dimensions Name=FunctionName,Value=nfe-api

# Se > 10s consistente: rollback
```

## 📚 Documentos Relacionados

- [PRODUCTION_ACTIVATION.md](./PRODUCTION_ACTIVATION.md)
- [MONITORING_GUIDE.md](./MONITORING_GUIDE.md)
- [INCIDENT_RESPONSE.md](./INCIDENT_RESPONSE.md)
- [DISASTER_RECOVERY.md](./DISASTER_RECOVERY.md)

---

> **⚠️ LEMBRETE**: Rollback é preferível a dados fiscais incorretos. Sempre opte pela segurança. Versão: 1.0 | Atualização: 2025-01-20