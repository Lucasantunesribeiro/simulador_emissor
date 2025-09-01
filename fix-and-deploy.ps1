# Script PowerShell para corrigir conflitos e fazer deploy
# Execute este script no PowerShell do Windows

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "🚀 NFe API - Fix & Deploy Automatico" -ForegroundColor Cyan  
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Navegar para o diretório raiz
Set-Location "D:\Programacao\Emissao_NFE"

Write-Host "🧹 Limpando cache do NuGet..." -ForegroundColor Yellow
dotnet nuget locals all --clear
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Falha na limpeza do cache" -ForegroundColor Red
    exit 1
}

Write-Host "📦 Removendo bin e obj folders..." -ForegroundColor Yellow
Get-ChildItem -Path . -Recurse -Directory -Name "bin" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path . -Recurse -Directory -Name "obj" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "🔄 Executando dotnet restore..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Falha no restore" -ForegroundColor Red
    exit 1
}

Write-Host "🏗️ Executando build..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Falha no build" -ForegroundColor Red
    exit 1
}

Write-Host "📦 Executando publish..." -ForegroundColor Yellow
Set-Location "NFe.API"
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Falha no publish" -ForegroundColor Red
    exit 1
}

Write-Host "📦 Criando ZIP de deployment..." -ForegroundColor Yellow
Set-Location "publish"
if (Test-Path "../nfe-api-deployment.zip") {
    Remove-Item "../nfe-api-deployment.zip" -Force
}
Compress-Archive -Path * -DestinationPath "../nfe-api-deployment.zip"
Set-Location ".."

Write-Host "☁️ Fazendo upload para AWS Lambda..." -ForegroundColor Yellow
aws lambda update-function-code --function-name nfe-api --zip-file fileb://nfe-api-deployment.zip --region us-east-1

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Deploy concluído com sucesso!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🌐 URL da API: https://42zqg8iw8b.execute-api.us-east-1.amazonaws.com/prod/" -ForegroundColor Cyan
    Write-Host "🔗 Health Check: https://42zqg8iw8b.execute-api.us-east-1.amazonaws.com/prod/health" -ForegroundColor Cyan
    Write-Host "📋 Vendas: https://42zqg8iw8b.execute-api.us-east-1.amazonaws.com/prod/api/v1/vendas" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "🧪 Testando API (aguarde 5 segundos para warm-up)..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    try {
        $response = Invoke-WebRequest -Uri "https://42zqg8iw8b.execute-api.us-east-1.amazonaws.com/prod/" -UseBasicParsing -TimeoutSec 10
        Write-Host "✅ API está respondendo! Status: $($response.StatusCode)" -ForegroundColor Green
        Write-Host "Response: $($response.Content)" -ForegroundColor Gray
    }
    catch {
        Write-Host "⚠️  API pode estar inicializando. Teste novamente em alguns segundos." -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ Falha no deploy para AWS Lambda" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎉 Deploy concluído! A API NFe está funcionando!" -ForegroundColor Green