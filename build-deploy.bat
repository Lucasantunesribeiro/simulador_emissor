@echo off
echo ========================================
echo 🚀 NFe API - Build e Deploy
echo ========================================
echo.

cd NFe.API

echo 📦 Executando dotnet restore...
dotnet restore
if %errorlevel% neq 0 (
    echo ❌ Falha no restore
    pause
    exit /b 1
)

echo 🔨 Executando dotnet publish...
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
if %errorlevel% neq 0 (
    echo ❌ Falha no publish
    pause
    exit /b 1
)

echo 📦 Criando ZIP de deployment...
cd publish
powershell -Command "Compress-Archive -Path * -DestinationPath ../nfe-api-deployment.zip -Force"
cd ..

echo ☁️ Fazendo upload para AWS Lambda...
aws lambda update-function-code --function-name nfe-api --zip-file fileb://nfe-api-deployment.zip --region us-east-1

if %errorlevel% equ 0 (
    echo.
    echo ✅ Deploy concluído com sucesso!
    echo.
    echo 🌐 URL da API: https://42zqg8iw8b.execute-api.us-east-1.amazonaws.com/prod/
    echo 🔗 Health Check: https://42zqg8iw8b.execute-api.us-east-1.amazonaws.com/prod/health
    echo 📋 Vendas: https://42zqg8iw8b.execute-api.us-east-1.amazonaws.com/prod/api/v1/vendas
    echo.
    echo 🧪 Testando API...
    timeout 3 >nul
    curl -s https://42zqg8iw8b.execute-api.us-east-1.amazonaws.com/prod/
    echo.
) else (
    echo ❌ Falha no deploy para AWS
)

echo.
pause