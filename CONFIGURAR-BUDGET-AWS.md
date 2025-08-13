# 🏦 Como Configurar Limite de $1 na AWS

## Método Recomendado: Via Console AWS

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

## ✅ Verificação

Após configurar, você deve:
- ✅ Ver o budget "MonthlyBudget1USD" no console
- ✅ Receber email de confirmação da AWS
- ✅ Receber alertas quando atingir 80% ($0.80) e 100% ($1.00)

## 🚨 Importante

- **Limite de $1**: Muito baixo para desenvolvimento ativo
- **Recomendação**: Considere $5-10 para desenvolvimento confortável
- **Free Tier**: Muitos serviços são gratuitos nos primeiros 12 meses

## 📊 Serviços Gratuitos (Free Tier - 12 meses):
- ✅ Lambda: 1M requests/mês
- ✅ RDS: 750h db.t3.micro/mês  
- ✅ API Gateway: 1M requests/mês
- ✅ CloudWatch: Logs básicos

## 🎯 Dicas para Manter Dentro do Limite

1. **Use Free Tier**: Mantenha-se dentro dos limites
2. **Monitore**: Verifique billing dashboard semanalmente
3. **Cleanup**: Remova recursos não utilizados
4. **Alertas**: Configure alertas em 50% também
5. **Região**: Use us-east-1 (mais barata)