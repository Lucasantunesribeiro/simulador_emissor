# Project Structure

## Solution Organization
The solution follows Clean Architecture principles with clear separation of concerns across 5 main projects:

### NFe.API
**Web API Layer** - Controllers, endpoints, middleware
- `Controllers/`: REST API controllers
- `Program.cs`: Application startup and DI configuration
- `appsettings.json`: Configuration files
- `Dockerfile`: API containerization
- Exposes REST endpoints for sales and protocol management
- Includes Swagger documentation and health checks

### NFe.Core
**Domain Layer** - Business logic and contracts
- `Entities/`: Domain models (Venda, Protocolo, etc.)
- `Interfaces/`: Service and repository contracts
- `Services/`: Business logic implementation
- Contains no external dependencies
- Defines the core business rules and domain models

### NFe.Infrastructure
**Infrastructure Layer** - External integrations and data access
- `Repositories/`: Data access implementations
- `Sefaz/`: SEFAZ integration services
- `Security/`: Digital signature and certificate handling
- Implements interfaces defined in Core layer
- Handles external system integrations

### NFe.Worker
**Background Service** - Async processing
- `Worker.cs`: Background service implementation
- `NFeWorker.cs`: NF-e specific processing logic
- Processes pending sales automatically
- Runs as a separate containerized service

### NFe.Tests
**Test Layer** - Unit and integration tests
- `UnitNFeServiceTests.cs`: Service layer tests
- Uses xUnit framework
- Focuses on Core business logic testing

## Key Conventions

### Dependency Flow
- API → Infrastructure → Core
- Worker → Infrastructure → Core
- Tests → Core
- Core has no external dependencies

### Naming Patterns
- Interfaces: `I{ServiceName}` (e.g., `IVendaService`)
- Services: `{DomainName}Service` (e.g., `VendaService`)
- Repositories: `{EntityName}Repository` (e.g., `VendaRepository`)
- Controllers: `{EntityName}Controller` with `api/v1/` routing

### Configuration
- Environment-specific settings in `appsettings.{Environment}.json`
- Docker environment variables override appsettings
- Simulation mode controlled via configuration flags