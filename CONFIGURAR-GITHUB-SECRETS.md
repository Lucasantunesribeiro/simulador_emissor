# 🔐 Configurar GitHub Secrets para Deploy AWS

## ❌ **Erro Atual**
O GitHub Actions está falhando porque faltam os secrets necessários para deploy na AWS.

**Erro**: `Input required and not supplied: aws-region`

## ✅ **Solução: Configurar Secrets**

### **1. Acessar Configurações do Repositório**
1. Vá para: https://github.com/Lucasantunesribeiro/simulador_emissor
2. Clique em **"Settings"** (aba do repositório)
3. No menu lateral, clique em **"Secrets and variables"** → **"Actions"**

### **2. Adicionar Secrets Necessários**

Clique em **"New repository secret"** para cada um:

#### **AWS_ACCESS_KEY_ID**
- **Name**: `AWS_ACCESS_KEY_ID`
- **Secret**: `AKIAQHZXVN6RKQO7RKQO` (sua chave de acesso AWS)

#### **AWS_SECRET_ACCESS_KEY**
- **Name**: `AWS_SECRET_ACCESS_KEY`  
- **Secret**: `sua-secret-key-aws` (sua chave secreta AWS)

### **3. Região AWS (Já Configurada)**
A região `us-east-1` já está hardcoded no workflow, então não precisa de secret.

## 🔍 **Como Obter as Credenciais AWS**

### **Opção 1: Usar Credenciais Existentes**
Se você já tem as credenciais configuradas localmente:

```bash
# Ver credenciais atuais (mascaradas)
aws configure list

# Ver chave de acesso
aws configure get aws_access_key_id

# A secret key não é mostrada por segurança
```

### **Opção 2: Criar Novas Credenciais**
1. Acesse: https://console.aws.amazon.com/iam/home#/users
2. Clique no seu usuário (`nfe-deploy-user`)
3. Aba **"Security credentials"**
4. **"Create access key"** → **"Command Line Interface (CLI)"**
5. Copie **Access key ID** e **Secret access key**

## ⚠️ **Importante: Segurança**

### **Permissões Mínimas**
O usuário deve ter apenas as permissões necessárias:
- `AWSLambdaFullAccess` (para deploy das funções)
- `IAMReadOnlyAccess` (para verificar roles)

### **Rotação de Chaves**
- ✅ Use chaves específicas para CI/CD
- ✅ Rotacione periodicamente
- ✅ Monitore uso no CloudTrail

## 🚀 **Após Configurar os Secrets**

1. **Teste o Workflow**:
   - Faça um commit qualquer
   - Verifique se o deploy funciona

2. **Monitore os Logs**:
   - Vá em **Actions** no GitHub
   - Acompanhe o progresso do deploy

3. **Verifique na AWS**:
   - Confirme se as funções Lambda foram atualizadas
   - Teste os endpoints da API

## 🔧 **Workflow Atualizado**

O workflow foi corrigido para:
- ✅ **Remover testes**: Não há mais pasta `NFe.Tests`
- ✅ **Região fixa**: `us-east-1` hardcoded
- ✅ **Build otimizado**: .NET 9 com Release
- ✅ **Deploy melhorado**: Logs mais claros
- ✅ **Verificação**: Confirma deploy bem-sucedido

## 📋 **Checklist**

- [ ] Configurar `AWS_ACCESS_KEY_ID` no GitHub
- [ ] Configurar `AWS_SECRET_ACCESS_KEY` no GitHub  
- [ ] Fazer commit para testar o workflow
- [ ] Verificar logs do GitHub Actions
- [ ] Confirmar deploy na AWS Lambda
- [ ] Testar API após deploy

## 🆘 **Se Ainda Houver Problemas**

1. **Verificar Credenciais**:
   ```bash
   aws sts get-caller-identity
   ```

2. **Testar Localmente**:
   ```bash
   aws lambda list-functions --query 'Functions[?starts_with(FunctionName, `nfe`)].FunctionName'
   ```

3. **Logs Detalhados**:
   - Verifique os logs completos no GitHub Actions
   - Procure por mensagens de erro específicas

**🎯 Após configurar os secrets, o deploy deve funcionar automaticamente!**