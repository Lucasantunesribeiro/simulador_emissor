# ✅ CHECKLIST DE PRODUÇÃO NFe

> **CRÍTICO**: Este checklist DEVE ser completado 100% antes da ativação de produção. Cada item é obrigatório.

## 🔐 SEGURANÇA E CERTIFICADOS

### Certificado Digital A1
- [ ] Certificado A1 válido e ativo
- [ ] Data de expiração > 90 dias
- [ ] Certificado testado em homologação SEFAZ
- [ ] Certificado armazenado no AWS Secrets Manager
- [ ] Senha do certificado no AWS Secrets Manager
- [ ] Permissões de acesso configuradas corretamente
- [ ] Certificado renovável antes do vencimento

**Validação**:
```bash
aws secretsmanager describe-secret --secret-id nfe-prod-certificate
openssl pkcs12 -info -in certificado.p12 -noout
```

### Chaves e Secrets
- [ ] JWT Secret Key (256 bits mínimo)
- [ ] Senhas de banco de dados seguras
- [ ] API Keys rotacionáveis
- [ ] Nenhuma credencial hardcoded no código
- [ ] Secrets Manager configurado corretamente
- [ ] Rotação automática de secrets habilitada

**Validação**:
```bash
aws secretsmanager list-secrets --query 'SecretList[?contains(Name, `nfe-prod`)]'
```

## 🏢 DADOS DO EMITENTE

### Informações Obrigatórias
- [ ] **CNPJ**: Válido e ativo na Receita Federal
- [ ] **Razão Social**: Exatamente como no CNPJ
- [ ] **Nome Fantasia**: Correto (se aplicável)
- [ ] **Inscrição Estadual**: Válida no estado emissor
- [ ] **Regime Tributário**: Configurado corretamente (1=Simples, 2=Simples Excesso, 3=Regime Normal)
- [ ] **Endereço Completo**: Logradouro, número, bairro
- [ ] **CEP**: Válido e no formato correto
- [ ] **Município**: Código IBGE correto
- [ ] **UF**: Estado correto

**Validação**:
```bash
# Verificar CNPJ na Receita Federal
curl "https://www.receitaws.com.br/v1/cnpj/${SEFAZ_CNPJ}"

# Validar configurações
echo "CNPJ: $SEFAZ_CNPJ"
echo "Razão Social: $SEFAZ_RAZAO_SOCIAL"
echo "IE: $SEFAZ_INSCRICAO_ESTADUAL"
```

## 🌐 CONECTIVIDADE SEFAZ

### Ambiente de Produção
- [ ] URLs de produção SEFAZ configuradas
- [ ] Conectividade testada com sucesso
- [ ] Timeout configurado adequadamente (10 minutos)
- [ ] Retry policy implementada (3 tentativas)
- [ ] Status do serviço SEFAZ verificado
- [ ] Webservices de produção acessíveis
- [ ] Firewall liberado para IPs da SEFAZ

**Validação**:
```bash
# Testar conectividade SEFAZ SP Produção
curl -I --connect-timeout 10 "https://nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx"

# Verificar status do serviço
curl -X POST "https://nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx" \
  -H "Content-Type: text/xml" \
  -d @status_service_request.xml
```

### Ambiente e Configurações
- [ ] **Ambiente SEFAZ**: 1 (Produção)
- [ ] **UseReal**: true
- [ ] **Série NFe**: Definida e liberada na SEFAZ
- [ ] **Numeração**: Sequencial configurada
- [ ] **Contingência**: Procedimentos definidos

## 🗄️ BANCO DE DADOS

### Infraestrutura RDS
- [ ] **Multi-AZ**: Habilitado para alta disponibilidade
- [ ] **Backups Automáticos**: Habilitados (30 dias retenção)
- [ ] **Point-in-time Recovery**: Habilitado
- [ ] **Performance Insights**: Habilitado
- [ ] **Enhanced Monitoring**: Habilitado
- [ ] **Encryption at Rest**: Habilitado
- [ ] **Encryption in Transit**: SSL obrigatório

**Validação**:
```bash
aws rds describe-db-instances --db-instance-identifier nfe-production-db
```

### Schema e Migrations
- [ ] Todas as migrations aplicadas
- [ ] Índices de performance criados
- [ ] Constraints de integridade implementadas
- [ ] Dados de seed carregados
- [ ] Procedures e functions testadas
- [ ] Triggers funcionando corretamente

**Validação**:
```sql
-- Verificar migrations
SELECT * FROM __efmigrationshistory ORDER BY migration_id;

-- Verificar índices críticos
SELECT schemaname, tablename, indexname 
FROM pg_indexes 
WHERE tablename IN ('vendas', 'nfe_protocolos', 'usuarios');
```

## ☁️ INFRAESTRUTURA AWS

### Lambda Functions
- [ ] **Environment**: Production
- [ ] **Memory**: Adequada para carga (mínimo 1GB)
- [ ] **Timeout**: Configurado (15 minutos para NFe)
- [ ] **Dead Letter Queue**: Configurada
- [ ] **VPC**: Configurada se necessária
- [ ] **IAM Roles**: Permissões mínimas necessárias
- [ ] **Environment Variables**: Todas configuradas

**Validação**:
```bash
aws lambda list-functions --query 'Functions[?contains(FunctionName, `nfe`)]'
aws lambda get-function-configuration --function-name nfe-api
```

### API Gateway
- [ ] **Custom Domain**: Configurado
- [ ] **SSL Certificate**: Válido e configurado
- [ ] **Rate Limiting**: Configurado adequadamente
- [ ] **CORS**: Configurado para domínios de produção
- [ ] **API Keys**: Configuradas se necessárias
- [ ] **Logging**: Habilitado
- [ ] **Monitoring**: Habilitado

### SQS e Mensageria
- [ ] **Queue de Produção**: Criada e configurada
- [ ] **Dead Letter Queue**: Configurada
- [ ] **Visibility Timeout**: Adequado
- [ ] **Message Retention**: Configurado (14 dias)
- [ ] **Encryption**: Habilitada
- [ ] **Access Policy**: Configurada corretamente

## 📊 MONITORAMENTO E ALERTAS

### CloudWatch
- [ ] **Log Groups**: Criados para todos os serviços
- [ ] **Log Retention**: 90 dias mínimo
- [ ] **Custom Metrics**: Implementadas
- [ ] **Dashboards**: Criados e funcionais
- [ ] **Structured Logging**: Implementado

**Validação**:
```bash
aws logs describe-log-groups --log-group-name-prefix "/aws/lambda/nfe"
```

### Alarmes Críticos
- [ ] **Taxa de Erro**: > 5% em 5 minutos
- [ ] **Latência**: > 10 segundos
- [ ] **Falhas SEFAZ**: > 3 falhas consecutivas
- [ ] **Memory/CPU**: > 80% por 10 minutos
- [ ] **Dead Letter Queue**: Mensagens acumuladas
- [ ] **Certificate Expiry**: 30 dias antes do vencimento

### SNS e Notificações
- [ ] **Tópicos de Alerta**: Criados
- [ ] **Subscriptions**: Configuradas para equipe
- [ ] **Escalation**: Configurada por severidade
- [ ] **Email/SMS**: Testados e funcionais

## 🔒 SEGURANÇA

### WAF e Proteção
- [ ] **AWS WAF**: Configurado na API Gateway
- [ ] **Rate Limiting**: 100 req/min por IP
- [ ] **SQL Injection**: Proteção ativa
- [ ] **XSS Protection**: Ativa
- [ ] **IP Allowlist**: Configurada se necessária

### Headers de Segurança
- [ ] **HSTS**: Habilitado
- [ ] **CSP**: Configurado
- [ ] **X-Frame-Options**: DENY
- [ ] **X-Content-Type-Options**: nosniff
- [ ] **Referrer-Policy**: strict-origin-when-cross-origin

### Auditoria
- [ ] **CloudTrail**: Habilitado
- [ ] **Config**: Monitoramento de mudanças
- [ ] **Access Logs**: Habilitados
- [ ] **VPC Flow Logs**: Habilitados se aplicável

## 🧪 TESTES

### Testes de Integração
- [ ] Todos os testes E2E passando
- [ ] Testes de carga executados
- [ ] Testes de stress executados
- [ ] Testes de failover executados
- [ ] Rollback testado com sucesso

### Testes NFe Específicos
- [ ] Emissão em homologação testada
- [ ] Cancelamento testado
- [ ] Consulta de status testada
- [ ] Inutilização testada
- [ ] Contingência testada

**Validação**:
```bash
# Executar testes E2E
dotnet test NFe.Tests.E2E --configuration Production

# Testes de carga
artillery run load-test-production.yml
```

## 🚀 DEPLOY E CI/CD

### Pipeline de Produção
- [ ] **GitHub Actions**: Workflow de produção configurado
- [ ] **Secrets**: Configurados no GitHub
- [ ] **Deploy Automático**: Desabilitado (deploy manual)
- [ ] **Rollback Capability**: Testada
- [ ] **Blue/Green**: Configurado se aplicável

### Versionamento
- [ ] **Tag de Release**: Criada
- [ ] **Changelog**: Atualizado
- [ ] **Database Migrations**: Versionadas
- [ ] **Backward Compatibility**: Garantida

## 📝 DOCUMENTAÇÃO

### Documentos Obrigatórios
- [ ] [PRODUCTION_ACTIVATION.md](./PRODUCTION_ACTIVATION.md) atualizado
- [ ] [ROLLBACK_PROCEDURES.md](./ROLLBACK_PROCEDURES.md) atualizado
- [ ] [MONITORING_GUIDE.md](./MONITORING_GUIDE.md) criado
- [ ] **API Documentation**: Atualizada para produção
- [ ] **Runbooks**: Criados para operações

### Conformidade e Legal
- [ ] **LGPD**: Compliance verificado
- [ ] **Auditoria Fiscal**: Logs adequados
- [ ] **Retenção de Dados**: Políticas implementadas
- [ ] **Backup Legal**: Estratégia definida (5 anos)

## 🎯 VALIDAÇÃO FINAL

### Checklist de Go-Live
- [ ] **Todos os itens acima**: ✅ Verificados
- [ ] **Equipe de Plantão**: Escalada e disponível
- [ ] **Rollback Plan**: Testado e documentado
- [ ] **Comunicação**: Stakeholders notificados
- [ ] **Janela de Manutenção**: Agendada se necessária

### Sign-off Obrigatório

| Função | Nome | Assinatura | Data |
|--------|------|------------|------|
| **Backend Lead** | | | |
| **DevOps Engineer** | | | |
| **Security Officer** | | | |
| **Product Owner** | | | |
| **CTO** | | | |

### Comando de Validação Completa
```bash
# Execute este script para validação automática
./scripts/validate-production-readiness.sh

# Deve retornar: "✅ Sistema pronto para ativação de produção!"
```

---

## 🚨 IMPORTANTE

**⚠️ ESTE CHECKLIST É OBRIGATÓRIO**

- Nenhum item pode ser pulado
- Todos os testes devem passar 100%
- Sign-off de todas as funções obrigatório
- Documentação completa é mandatória
- Rollback deve estar testado e pronto

**📞 Em caso de dúvidas, pare o processo e consulte a equipe sênior.**

---

> **Status**: 🔴 NÃO VALIDADO | **Versão**: 1.0 | **Atualização**: 2025-01-20