# Nexora

> **Unify. Automate. Scale.**

Nexora is a modular, multi-tenant enterprise platform for managing CRM, donations, sponsorships, education, finance, HR, and more — from a single, unified system.

## Key Features

- **Modular Architecture**: Install only the modules you need
- **Multi-Tenant**: Single deployment serves multiple organizations with complete data isolation
- **360° Contact View**: One contact record across all modules
- **Self-Service Portals**: Branded portals for donors, parents, and volunteers
- **Event-Driven**: Real-time updates across modules via Kafka
- **White-Label Ready**: Custom branding, domains, and themes per tenant

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, ASP.NET Core, EF Core |
| Database | PostgreSQL 17 (schema-per-tenant) |
| Cache | Redis |
| Events | Apache Kafka (via Dapr) |
| Auth | Keycloak (OIDC/OAuth2) |
| API Gateway | Apache APISIX |
| Secrets | HashiCorp Vault |
| Storage | MinIO (S3-compatible) |
| Frontend | React 19 (Admin), Next.js 16 (Portal) |
| Observability | OpenTelemetry, Grafana, Loki, Tempo |

## Modules

| Module | Description |
|--------|-------------|
| Identity & Access | Multi-tenant auth, RBAC, organization management |
| Contacts | Unified contact registry |
| CRM | Leads, pipelines, campaigns |
| Donations | Online giving, recurring, receipts, fund tracking |
| Sponsorship | Sponsor-beneficiary matching, installments |
| Education | Enrollment, student records, parent portal |
| Subscription | Recurring billing, tuition management |
| Accounting | Journals, expenses, bank reconciliation |
| Projects | Task management, construction cost tracking |
| HR | Employee records, contracts, payroll |
| Events | Seasonal campaigns, registrations |
| POS | Touch-friendly sales terminal |
| Fleet | Vehicle tracking, maintenance |
| Inventory | Stock management, asset tracking |

## Documentation

See [docs/README.md](docs/README.md) for the full documentation index.

## Getting Started

```bash
# Clone the repository
git clone https://github.com/cemililik/Nexora.git
cd Nexora

# Start infrastructure (PostgreSQL, Redis, Kafka, Keycloak, etc.)
docker compose up -d

# Run the application
dotnet run --project src/Nexora.Host
```

## License

Proprietary — All rights reserved.
