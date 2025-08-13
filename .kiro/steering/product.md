# Product Overview

## NFe Emitter - Automated Electronic Invoice System

This is a complete automated Electronic Invoice (NF-e) emission system built for the Brazilian market. The system handles the entire lifecycle of electronic invoices including creation, digital signature, SEFAZ integration, and status tracking.

### Key Features
- Automated NF-e generation and emission
- SEFAZ (Brazilian tax authority) integration
- Digital signature support with Azure Key Vault integration
- Background processing for pending sales
- Health monitoring and observability
- Docker containerization ready
- CI/CD pipeline configured

### Business Domain
- **Sales Management**: Create and track sales transactions
- **Invoice Processing**: Generate XML invoices following Brazilian NF-e standards
- **Tax Compliance**: Integration with SEFAZ for official validation and approval
- **Protocol Management**: Track submission protocols and access keys

### Simulation vs Production
The system runs in simulation mode by default for development and testing. Production deployment requires:
- Real SEFAZ endpoint configuration
- Azure Key Vault certificate integration
- Persistent database implementation
- Real digital signature implementation