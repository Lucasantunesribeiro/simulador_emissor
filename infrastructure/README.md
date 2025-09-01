# NFe Infrastructure - AWS CDK

Infraestrutura como código para o sistema de emissão de Notas Fiscais Eletrônicas (NFe) usando AWS CDK com TypeScript.

## 🏗️ Arquitetura

### Visão Geral
```
Internet
    ↓
[WAF] → [API Gateway] → [Lambda API] ← [VPC]
                                       ↓
                              [Lambda Worker] ← [SQS]
                                       ↓
                                [RDS PostgreSQL]
                                       ↓
                              [S3] + [Secrets Manager]
```

### Componentes Principais

#### **VPC e Networking**
- **VPC** com 3 subnets por AZ (Public, Private, Database)
- **NAT Gateway** para acesso à internet das subnets privadas
- **VPC Endpoints** para S3 e Secrets Manager (otimização de custos)
- **Security Groups** com princípio do menor privilégio

#### **Compute**
- **Lambda API**: Função .NET 8 para API REST (30s timeout)
- **Lambda Worker**: Função .NET 8 para processamento assíncrono (15min timeout)
- **API Gateway** com throttling e logging configurados

#### **Storage e Databases**
- **RDS PostgreSQL 16**: Multi-AZ para produção, single-AZ para dev
- **S3 Buckets**:
  - `nfe-xmls-{stage}`: Armazenamento de XMLs das NFe
  - `nfe-danfes-{stage}`: Armazenamento de DANFEs (PDF)
  - `nfe-logs-{stage}`: Logs estruturados da aplicação

#### **Message Queuing**
- **SQS Main Queue**: Fila principal de processamento
- **SQS DLQ**: Dead Letter Queue para mensagens com falha

#### **Security**
- **WAF**: Proteção da API com regras gerenciadas da AWS
- **Secrets Manager**: Armazenamento seguro de certificados A1
- **IAM Roles**: Permissions granulares por serviço
- **Encryption**: At-rest em todos os recursos

#### **Monitoring**
- **CloudWatch**: Logs estruturados e métricas
- **CloudWatch Alarms**: Monitoramento proativo
- **SNS**: Notificações de alertas
- **X-Ray**: Tracing distribuído

## 🚀 Deploy

### Pré-requisitos

```bash
# AWS CLI configurado
aws configure

# Node.js 18+ e npm
node --version
npm --version

# AWS CDK
npm install -g aws-cdk

# .NET 8 SDK
dotnet --version
```

### Configuração Inicial

1. **Instalar dependências**:
```bash
cd infrastructure
npm install
```

2. **Bootstrap do CDK** (primeira vez):
```bash
# Para desenvolvimento
cdk bootstrap --profile default

# Para produção
cdk bootstrap --profile production
```

3. **Build das aplicações .NET**:
```bash
# API
cd ../NFe.API
dotnet publish -c Release -o bin/Release/net8.0/publish

# Worker
cd ../NFe.Worker  
dotnet publish -c Release -o bin/Release/net8.0/publish
```

### Deployment para Desenvolvimento

```bash
cd infrastructure
./deploy-dev.sh
```

O script irá:
- ✅ Verificar pré-requisitos
- 🔨 Build do projeto CDK
- 📦 Build das aplicações .NET
- 🔍 Mostrar diferenças
- ❓ Solicitar confirmação
- 🚀 Deploy da infraestrutura
- 📋 Exibir outputs da stack

### Deployment para Produção

```bash
cd infrastructure
./deploy-prod.sh
```

**⚠️ ATENÇÃO**: Deployment de produção inclui verificações adicionais:
- Confirmação de testes em staging
- Confirmação de backup de dados
- Verificação de janela de manutenção
- Dupla confirmação com texto específico
- Testes pós-deploy automatizados

### Destruição de Ambiente

```bash
# Desenvolvimento
./destroy.sh dev

# Produção (muito cuidado!)
./destroy.sh prod
```

## ⚙️ Configuração

### Variáveis de Ambiente

As Lambda functions recebem as seguintes variáveis automaticamente:

```bash
# Ambiente
ASPNETCORE_ENVIRONMENT=Development|Production

# Database
ConnectionStrings__DefaultConnection=Host=...;Port=5432;Database=nfedb;Username={username};Password={password}

# AWS
AWS_REGION=us-east-1

# S3 Buckets
S3_XMLS_BUCKET=nfe-xmls-dev-123456789
S3_DANFES_BUCKET=nfe-danfes-dev-123456789
S3_LOGS_BUCKET=nfe-logs-dev-123456789

# SQS
SQS_PROCESSING_QUEUE=https://sqs.us-east-1.amazonaws.com/123456789/nfe-processing-dev

# Secrets
SECRETS_CERTIFICATE=nfe-certificate-a1-dev
SECRETS_DATABASE=nfe-database-credentials-dev
```

### Certificados A1

Após o deploy, configure os certificados A1 no Secrets Manager:

```bash
# Obter o nome do secret
aws cloudformation describe-stacks \
  --stack-name NFeInfrastructureStack-dev \
  --query 'Stacks[0].Outputs[?contains(OutputKey, `Certificate`)].OutputValue' \
  --output text

# Atualizar o secret (substitua pelos seus certificados reais)
aws secretsmanager put-secret-value \
  --secret-id nfe-certificate-a1-dev \
  --secret-string '{
    "certificatePem": "-----BEGIN CERTIFICATE-----\n...\n-----END CERTIFICATE-----",
    "privateKeyPem": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----", 
    "password": "sua-senha-do-certificado"
  }'
```

### Database Migrations

Execute as migrations após o primeiro deploy:

```bash
# Obter connection string do RDS
aws rds describe-db-instances \
  --db-instance-identifier nfeinfrastructurestack-dev-nfedatabase... \
  --query 'DBInstances[0].Endpoint.Address'

# Na aplicação .NET
cd ../NFe.API
dotnet ef database update
```

## 📊 Monitoramento

### CloudWatch Dashboards

Acesse o console da AWS e configure dashboards personalizados com:
- **Lambda**: Erros, duração, throttling
- **API Gateway**: Latência, 4XX/5XX errors, request count
- **RDS**: CPU, connections, disk usage
- **SQS**: Queue depth, DLQ messages

### Alarms Configurados

| Alarm | Threshold Dev | Threshold Prod | Description |
|-------|---------------|----------------|-------------|
| Lambda Errors | 10 | 5 | Taxa de erro muito alta |
| Lambda Duration | Variável | Variável | Tempo de execução alto |
| API Latency | 5000ms | 5000ms | Latência da API alta |
| DB CPU | 90% | 80% | CPU do banco alto |
| SQS Depth | 100 | 1000 | Muitas mensagens na fila |
| DLQ Messages | 1 | 1 | Mensagens na DLQ |

### Logs Estruturados

Os logs são enviados automaticamente para CloudWatch com:
- **Namespace**: `/aws/lambda/nfe-{api|worker}-{stage}`
- **Retention**: 1 semana (dev), 1 mês (prod)
- **X-Ray Tracing**: Habilitado para debugging

## 🔒 Segurança

### WAF Rules
- **AWS Managed Common Rule Set**: Proteção contra ataques comuns
- **Rate Limiting**: 500 req/5min (dev), 2000 req/5min (prod)
- **Regional WAF**: Proteção específica da região

### IAM Permissions
- **Lambda Execution Role**: Acesso granular aos recursos necessários
- **VPC Access**: Apenas para recursos dentro da VPC
- **Secrets Access**: Apenas aos secrets específicos do ambiente

### Network Security
- **Security Groups**: Regras restritivas por serviço
- **VPC**: Isolamento de rede
- **Private Subnets**: Lambda functions sem acesso direto à internet
- **Database Isolation**: RDS em subnet isolada

## 💰 Custos Estimados

### Ambiente de Desenvolvimento (us-east-1)
- **RDS t3.micro**: ~$15/mês
- **Lambda**: ~$5/mês (baseado em 10k invocações)
- **API Gateway**: ~$3/mês (baseado em 10k requests)
- **S3**: ~$5/mês (1GB de dados)
- **VPC/NAT**: ~$45/mês
- **Outros**: ~$7/mês
- **Total**: ~$80/mês

### Ambiente de Produção (us-east-1)
- **RDS t3.medium Multi-AZ**: ~$120/mês
- **Lambda**: ~$50/mês (baseado em 1M invocações)
- **API Gateway**: ~$35/mês (baseado em 1M requests)
- **S3**: ~$25/mês (100GB de dados)
- **VPC/NAT**: ~$90/mês (2 AZs)
- **Outros**: ~$30/mês
- **Total**: ~$350/mês

## 🐛 Troubleshooting

### Lambda Cold Start
- **Sintoma**: Primeira requisição lenta
- **Solução**: Considere Reserved Concurrency para produção

### RDS Connection Issues
- **Sintoma**: Timeout de conexão
- **Verificar**: Security Groups, VPC configuration
- **Logs**: CloudWatch Logs do Lambda

### SQS Messages não processadas
- **Sintoma**: Mensagens acumulando na fila
- **Verificar**: Logs do Worker Lambda
- **Ação**: Verificar DLQ para mensagens com erro

### Certificado A1 Inválido
- **Sintoma**: Erro de assinatura digital
- **Verificar**: Formato PEM no Secrets Manager
- **Ação**: Validar certificado e senha

## 📚 Referências

- [AWS CDK Documentation](https://docs.aws.amazon.com/cdk/)
- [AWS Well-Architected Framework](https://aws.amazon.com/architecture/well-architected/)
- [.NET on AWS Lambda](https://docs.aws.amazon.com/lambda/latest/dg/lambda-csharp.html)
- [NFe Manual Técnico](http://www.nfe.fazenda.gov.br/portal/principal.aspx)

## 🤝 Suporte

Para suporte e dúvidas:
- **Issues**: Abra uma issue no repositório
- **Documentation**: Consulte este README
- **AWS Support**: Para questões específicas da AWS

---

**⚠️ IMPORTANTE**: Este é um projeto de infraestrutura crítica. Sempre teste em ambiente de desenvolvimento antes de aplicar em produção.