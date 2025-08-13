# Technology Stack

## Framework & Runtime
- **.NET 9**: Latest .NET version with modern C# features
- **ASP.NET Core**: Web API framework
- **Worker Services**: Background processing

## Key Libraries & Packages
- **Unimake.DFe**: Brazilian electronic document framework for NF-e generation
- **Swashbuckle.AspNetCore**: OpenAPI/Swagger documentation
- **AspNetCore.HealthChecks.UI.Client**: Health monitoring
- **xUnit**: Unit testing framework
- **Microsoft.Extensions.*** : Dependency injection, logging, configuration

## Architecture Patterns
- **Clean Architecture**: Separation of concerns with Core, Infrastructure, API layers
- **Dependency Injection**: Built-in .NET DI container
- **Repository Pattern**: Data access abstraction
- **Background Services**: Worker pattern for async processing

## Development Tools
- **Docker**: Containerization with multi-stage builds
- **Docker Compose**: Local orchestration with SQL Server
- **GitHub Actions**: CI/CD pipeline ready

## Common Commands

### Build & Test
```bash
# Build entire solution
dotnet build

# Run tests
dotnet test NFe.Tests/NFe.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Running Services
```bash
# Run API locally
dotnet run --project NFe.API/NFe.API.csproj

# Run Worker service
dotnet run --project NFe.Worker/NFe.Worker.csproj

# Run with Docker Compose
docker-compose up -d

# View logs
docker-compose logs -f
```

### Development
```bash
# Restore packages
dotnet restore

# Clean build artifacts
dotnet clean

# Watch for changes (API)
dotnet watch --project NFe.API/NFe.API.csproj
```