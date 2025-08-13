# 🔧 Corrigir Erro de Credenciais AWS no GitHub Actions

## ❌ **Erro Atual**
```
Error: The security token included in the request is invalid.
```

## 🔍 **Diagnóstico**
- ✅ Suas credenciais locais estão funcionando
- ❌ As credenciais no GitHub estão inválidas/expiradas

## 🔐 **Solução: Atualizar Secrets do GitHub**

### **Passo 1: Obter Credenciais Atuais**

Suas credenciais locais funcionam, então vamos usá-las:

```bash
# Ver sua chave de acesso atual (mascarada)
aws configure get aws_access_key_id
# Resultado: AKIA...ZRWP

# Ver região
aws configure get region  
# Resultado: us-east-1
```

### **Passo 2: Acessar GitHub Secrets**

1. Vá para: https://github.com/Lucasantunesribeiro/simulador_emissor/settings/secrets/actions

2. **Atualizar ou Criar os Secrets:**

#### **AWS_ACCESS_KEY_ID**
- **Name**: `AWS_ACCESS_KEY_ID`
- **Value**: `AKIAQHZXVN6RKQOZRWP` (sua chave atual)

#### **AWS_SECRET_ACCESS_KEY**  
- **Name**: `AWS_SECRET_ACCESS_KEY`
- **Value**: `[SUA_SECRET_KEY_COMPLETA]` (não posso ver por segurança)

### **Passo 3: Como Obter a Secret Key**

#### **Opção A: Se você tem a chave salva**
- Procure em arquivos de configuração
- Verifique seu gerenciador de senhas
- Consulte anotações pessoais

#### **Opção B: Criar Nova Chave (Recomendado)**

1. **Acesse IAM Console**:
   - https://console.aws.amazon.com/iam/home#/users/nfe-deploy-user

2. **Criar Nova Access Key**:
   - Aba "Security credentials"
   - "Create access key"
   - Selecione "Command Line Interface (CLI)"
   - Marque "I understand..."
   - Clique "Create access key"

3. **Copiar Credenciais**:
   - **Access key ID**: `AKIA...` (nova)
   - **Secret access key**: `...` (copie imediatamente)

4. **⚠️ IMPORTANTE**: Salve as credenciais imediatamente, pois a secret key só é mostrada uma vez!

### **Passo 4: Atualizar Secrets no GitHub**

1. Vá para: https://github.com/Lucasantunesribeiro/simulador_emissor/settings/secrets/actions

2. **Para cada secret existente**:
   - Clique no lápis (editar)
   - Cole o novo valor
   - Clique "Update secret"

3. **Ou criar novos secrets**:
   - "New repository secret"
   - Nome e valor
   - "Add secret"

### **Passo 5: Testar o Deploy**

Após atualizar os secrets:

1. **Fazer um commit qualquer**:
   ```bash
   git commit --allow-empty -m "test: Testar deploy com credenciais atualizadas"
   git push origin main
   ```

2. **Verificar GitHub Actions**:
   - https://github.com/Lucasantunesribeiro/simulador_emissor/actions
   - Acompanhar o progresso do deploy

## 🔒 **Segurança: Rotação de Chaves**

### **Após Resolver o Problema**

1. **Desativar chaves antigas** (se criou novas)
2. **Atualizar credenciais locais**:
   ```bash
   aws configure set aws_access_key_id NOVA_CHAVE
   aws configure set aws_secret_access_key NOVA_SECRET
   ```

3. **Testar localmente**:
   ```bash
   aws sts get-caller-identity
   ```

## 🎯 **Checklist de Verificação**

- [ ] Obter credenciais AWS válidas
- [ ] Atualizar `AWS_ACCESS_KEY_ID` no GitHub
- [ ] Atualizar `AWS_SECRET_ACCESS_KEY` no GitHub
- [ ] Fazer commit de teste
- [ ] Verificar se deploy funciona
- [ ] Confirmar funções Lambda atualizadas

## 🆘 **Se Ainda Não Funcionar**

### **Verificar Permissões do Usuário**
```bash
# Testar se pode acessar Lambda
aws lambda list-functions --query 'Functions[?starts_with(FunctionName, `nfe`)].FunctionName'

# Verificar identidade
aws sts get-caller-identity
```

### **Logs Detalhados**
- Verifique os logs completos no GitHub Actions
- Procure por mensagens específicas de erro
- Compare com credenciais que funcionam localmente

## 🎉 **Resultado Esperado**

Após corrigir as credenciais, você deve ver:
```
✅ Build completed successfully
✅ API deployment completed  
✅ Worker deployment completed
✅ Deployment verification completed
```

**🔑 A chave é usar exatamente as mesmas credenciais que funcionam localmente!**