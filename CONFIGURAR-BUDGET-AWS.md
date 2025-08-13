# 🏦 Como Configurar Limite de $1 na AWS

## Método 1: Via Console AWS (Recomendado)

### Passo a Passo:

1. **Acesse o Console AWS**
   - Vá para: https://console.aws.amazon.com/billing/home#/budgets

2. **Criar Novo Budget**
   - Clique em "Create budget"
   - Selecione "Cost budget"
   - Clique em "Next"

3. **Configurar Budget**
   - **Budget name**: `MonthlyBudget1USD`
   - **Period**: Monthly
   - **Budget effective dates**: Recurring budget
   - **Start month**: Agosto 2025
   - **Budgeted amount**: `$1.00`

4. **Configurar Alertas**
   - **Alert 1**:
     - Threshold: 80% of budgeted amount
     - Email: `lucasantunesribeiro@gmail.com`
   - **Alert 2**:
     - Threshold: 100% of budgeted amount  
     - Email: `lucasantunesribeiro@gmail.com`

5. **Finalizar**
   - Review e clique em "Create budget"

## Método 2: Via AWS CLI (Se o ambiente estiver funcionando)

```bash
# 1. Obter Account ID
aws sts get-caller-identity --query Account --output text

# 2. Criar budget
aws budgets create-budget \
  --account-id SEU_ACCOUNT_ID \
  --budget file://budget-config.json \
  --notifications-with-subscribers file://notifications-config.json
```

## ✅ Verificação

Após configurar, você deve:
- ✅ Ver o budget "MonthlyBudget1USD" no console
- ✅ Receber email de confirmação da AWS
- ✅ Receber alertas quando atingir 80% ($0.80) e 100% ($1.00)

## 🚨 Importante

- **Limite de $1**: Muito baixo para desenvolvimento ativo
- **Recomendação**: Considere $5-10 para desenvolvimento confortável
- **Free Tier**: Muitos serviços são gratuitos nos primeiros 12 meses
- **Monitoramento**: Verifique regularmente o billing dashboard

## 📊 Serviços que Podem Gerar Custos

### Gratuitos (Free Tier - 12 meses):
- ✅ Lambda: 1M requests/mês
- ✅ RDS: 750h db.t3.micro/mês  
- ✅ API Gateway: 1M requests/mês
- ✅ CloudWatch: Logs básicos

### Que Podem Gerar Custos:
- ⚠️ RDS: Se exceder 750h/mês
- ⚠️ Lambda: Se exceder 1M requests
- ⚠️ Data Transfer: Tráfego de saída
- ⚠️ Storage: EBS, S3 além dos limites

## 🎯 Dicas para Manter Dentro do Limite

1. **Use Free Tier**: Mantenha-se dentro dos limites
2. **Monitore**: Verifique billing dashboard semanalmente
3. **Cleanup**: Remova recursos não utilizados
4. **Alertas**: Configure alertas em 50% também
5. **Região**: Use us-east-1 (mais barata)

## 📧 Contato

Se precisar de ajuda, o budget enviará alertas para:
**lucasantunesribeiro@gmail.com**