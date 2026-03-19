# Nexora - Architecture Overview

## 1. Architecture Style

**Modular Monolith → Microservices** (Evolutionary Architecture)

We start with a **Modular Monolith** approach and evolve to microservices as needed. Each module is a self-contained bounded context with clear interfaces, deployable independently when scale demands it.

### Why Not Microservices From Day One?
- Team size doesn't justify the operational overhead yet
- Module boundaries need to be discovered, not assumed
- Modular monolith gives us the same logical separation with simpler deployment
- Dapr sidecar pattern allows us to extract services later without code changes

### Evolutionary Path
```mermaid
---
title: Architecture Evolution
---
flowchart LR
    A["Phase 1\nModular Monolith\n(single deployable)"] --> B["Phase 2\nStrategic Extraction\n(high-traffic modules\nbecome services)"]
    B --> C["Phase 3\nFull Microservices\n(when team/scale\njustifies it)"]
    style A fill:#10b981,color:#fff
    style B fill:#f59e0b,color:#fff
    style C fill:#6366f1,color:#fff
```

## 2. High-Level Architecture

```mermaid
---
title: Nexora High-Level Architecture
---
flowchart TB
    Client["Client\n(Browser / Mobile)"]
    CDN["CDN / WAF\n(Cloudflare)"]
    Gateway["API Gateway\n(Apache APISIX)"]

    Portal["Web Portal\n(Next.js 16)"]
    Admin["Admin SPA\n(React 19)"]
    BFF["Mobile BFF\n(.NET API)"]

    Dapr["Dapr Sidecar\n(Service Mesh)"]

    Identity["Identity\nModule"]
    Contacts["Contact\nModule"]
    CRM["CRM\nModule"]
    Donations["Donation\nModule"]
    Finance["Finance\nModule"]
    Projects["Project\nModule"]
    More["...\nModules"]

    PG[("PostgreSQL\n(schema-per-tenant)")]
    Redis[("Redis\nCache")]
    Kafka[("Kafka\nEvents")]
    Keycloak["Keycloak\n(OIDC)"]
    MinIO[("MinIO\nObject Storage")]
    Vault["HashiCorp Vault\n(Secrets)"]

    Client --> CDN --> Gateway
    Gateway --> Portal & Admin & BFF
    Gateway -- "JWT validation" --> Keycloak
    Portal & Admin & BFF --> Dapr
    Dapr --> Identity & Contacts & CRM & Donations & Finance & Projects & More
    Identity & Contacts & CRM & Donations & Finance & Projects & More --> PG & Redis & Kafka
    Dapr -- "secrets" --> Vault
    Identity & Contacts & CRM & Donations & Finance & Projects & More -- "files" --> MinIO

    style Gateway fill:#e74c3c,color:#fff
    style Dapr fill:#9b59b6,color:#fff
    style PG fill:#336791,color:#fff
    style Redis fill:#dc382d,color:#fff
    style Kafka fill:#231f20,color:#fff
    style Keycloak fill:#4d4d4d,color:#fff
```

## 3. Technology Stack

### Backend
| Technology | Role | Why |
|-----------|------|-----|
| **.NET 10** | Application framework | High performance, mature ecosystem, strong typing, native AOT |
| **ASP.NET Core** | Web API framework | First-class OpenAPI, minimal APIs, gRPC support |
| **Entity Framework Core** | ORM | Code-first migrations, LINQ, multi-tenant query filters |
| **MediatR** | In-process messaging | CQRS pattern, clean module boundaries, pipeline behaviors |
| **FluentValidation** | Request validation | Declarative, testable validation rules |
| **Mapster** | Object mapping | Fastest .NET mapper, compile-time code generation |
| **Hangfire** | Background jobs | Scheduled tasks, recurring jobs, dashboard |

### Infrastructure & Middleware
| Technology | Role | Why |
|-----------|------|-----|
| **PostgreSQL 17** | Primary database | Multi-schema support (ideal for multi-tenancy), JSONB, full-text search |
| **Apache APISIX** | API Gateway | Rate limiting, auth, load balancing, plugin ecosystem, better than Kong for our scale |
| **Dapr** | Distributed runtime | Service invocation, pub/sub, state management, secrets — abstracts infrastructure |
| **Apache Kafka** | Event streaming | Cross-module events, audit log, data sync, replay capability |
| **Redis** | Caching & sessions | Distributed cache, rate limiting, real-time features |
| **HashiCorp Vault** | Secret management | API keys, DB credentials, encryption keys rotation |
| **Keycloak** | Identity provider | OAuth2/OIDC, social login, MFA, tenant-aware realms |
| **MinIO** | Object storage | S3-compatible, documents, images, attachments |

### Frontend
| Technology | Role | Why |
|-----------|------|-----|
| **React 19 + TypeScript** | Admin Dashboard SPA | Rich ecosystem, component libraries, strong community |
| **Next.js 16** | Public Portals & CMS | SSR/SSG for SEO, ISR for dynamic content, Turbopack stable, edge runtime |
| **Tailwind CSS 4** | Styling | Utility-first, themeable (white-label support), design system friendly |
| **shadcn/ui** | Component library | Accessible, customizable, not a dependency but owned code |
| **TanStack Query** | Server state | Caching, optimistic updates, real-time sync |
| **Zustand** | Client state | Lightweight, no boilerplate, TypeScript native |

### DevOps & Observability
| Technology | Role | Why |
|-----------|------|-----|
| **Docker** | Containerization | Consistent dev/prod environments |
| **Kubernetes (K8s)** | Orchestration | Auto-scaling, rolling deployments, self-healing |
| **Helm** | K8s package management | Templated deployments per environment |
| **GitHub Actions** | CI/CD | Native GitHub integration, matrix builds |
| **OpenTelemetry** | Distributed tracing | Vendor-agnostic observability |
| **Grafana + Loki + Tempo** | Monitoring stack | Logs, metrics, traces in one place |
| **SonarQube** | Code quality | Static analysis, security scanning |

### Communication & Integration
| Technology | Role | Why |
|-----------|------|-----|
| **Twilio / Netgsm** | SMS gateway | International + Turkey SMS support |
| **WhatsApp Business API** | WhatsApp messaging | Direct from CRM/Donation modules |
| **SendGrid / Mailgun** | Email delivery | Transactional + bulk email |
| **Stripe** | Payment processing | Global payments, recurring, multi-currency |
| **iyzico / Param** | Local payment (TR) | Turkish market payment support |

## 4. Multi-Tenancy Strategy

### Schema-per-Tenant (PostgreSQL)
```mermaid
---
title: PostgreSQL Schema-per-Tenant Layout
---
flowchart LR
    DB[("nexora_db")]
    DB --> Public["public schema\n(tenant registry,\nmodule catalog,\nsystem config)"]
    DB --> T1["tenant_acme schema\n(Org A tables)"]
    DB --> T2["tenant_isabet schema\n(Org B tables)"]
    DB --> T3["tenant_xyz schema\n(Org C tables)"]
    style Public fill:#f39c12,color:#fff
    style T1 fill:#3498db,color:#fff
    style T2 fill:#2ecc71,color:#fff
    style T3 fill:#9b59b6,color:#fff
```

**Why schema-per-tenant?**
- Strong data isolation (regulatory compliance)
- Independent backup/restore per tenant
- Tenant-specific migrations possible
- No row-level filtering overhead (vs. shared schema + tenant_id)
- PostgreSQL handles thousands of schemas efficiently

### Multi-Organization within Tenant
Within a tenant, multiple organizations (companies) share the same schema:
```mermaid
---
title: Multi-Organization within a Tenant
---
flowchart TB
    Tenant["Tenant: Isabet Group\n(tenant_isabet schema)"]
    Tenant --> Org1["Isabet Academy\n(organization_id: academy)"]
    Tenant --> Org2["IKF Foundation\n(organization_id: ikf)"]
    Tenant --> Org3["Isabet Catering\n(organization_id: catering)"]
    Tenant --> Org4["Isabet E-Commerce\n(organization_id: ecommerce)"]
    Shared["Shared Resources\n(Contacts, Products, Users)"]
    Tenant -.-> Shared
    style Tenant fill:#2c3e50,color:#fff
    style Shared fill:#95a5a6,color:#fff
```

## 5. Module Architecture

Each module follows **Clean Architecture** internally:

```
Nexora.Modules.CRM/
├── Domain/              # Entities, Value Objects, Domain Events
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/
│   └── Interfaces/
├── Application/         # Use Cases, Commands, Queries (CQRS)
│   ├── Commands/
│   ├── Queries/
│   ├── DTOs/
│   └── Validators/
├── Infrastructure/      # EF DbContext, External Services, Repositories
│   ├── Persistence/
│   ├── ExternalServices/
│   └── Configuration/
├── Api/                 # Controllers / Endpoints
│   ├── Endpoints/
│   └── Middleware/
└── Module.cs            # Module registration & dependency injection
```

### Module Communication
- **In-process**: MediatR notifications (domain events)
- **Cross-service**: Dapr pub/sub over Kafka (integration events)
- **Sync calls**: Dapr service invocation (when eventual consistency won't work)

## 6. Cross-Cutting Concerns

| Concern | Implementation |
|---------|---------------|
| Authentication | Keycloak OIDC → JWT validation in APISIX + .NET |
| Authorization | Permission-based RBAC with organization scope |
| Audit Logging | Kafka event stream → audit log consumer |
| Multi-tenancy | Tenant resolution middleware → schema switching |
| Caching | Redis with tenant-prefixed keys |
| Rate Limiting | APISIX plugin (per-tenant, per-API) |
| File Storage | MinIO with tenant-isolated buckets |
| Search | PostgreSQL full-text search (→ Elasticsearch when needed) |
| Localization | .NET resource files + DB-driven translations |
| Feature Flags | Custom implementation with Redis-backed store |

## 7. Deployment Architecture

```mermaid
---
title: Kubernetes Deployment Architecture
---
flowchart TB
    subgraph K8s["Kubernetes Cluster"]
        subgraph Ingress["Ingress Layer"]
            APISIX1["APISIX Gateway\n(replica 1)"]
            APISIX2["APISIX Gateway\n(replica 2)"]
        end

        subgraph App["Application Layer (HPA)"]
            Pod1["Nexora Pod 1\n+ Dapr Sidecar"]
            Pod2["Nexora Pod 2\n+ Dapr Sidecar"]
            Pod3["Nexora Pod N\n+ Dapr Sidecar"]
        end

        subgraph Data["Data Layer"]
            PG[("PostgreSQL\nHA Cluster")]
            Redis[("Redis\nCluster")]
            Kafka[("Kafka\nCluster")]
        end

        subgraph Platform["Platform Services"]
            KC["Keycloak (HA)"]
            MinIO[("MinIO")]
            Vault["Vault"]
        end
    end

    APISIX1 & APISIX2 --> Pod1 & Pod2 & Pod3
    Pod1 & Pod2 & Pod3 --> PG & Redis & Kafka
    Pod1 & Pod2 & Pod3 --> MinIO & Vault
    APISIX1 & APISIX2 -- "JWT\nvalidation" --> KC

    style K8s fill:#326ce5,color:#fff,stroke:#fff
    style Ingress fill:#e74c3c,color:#fff
    style App fill:#2ecc71,color:#fff
    style Data fill:#336791,color:#fff
    style Platform fill:#f39c12,color:#fff
```

## 8. Key Architecture Decisions Summary

| Decision | Choice | ADR |
|----------|--------|-----|
| Architecture style | Modular Monolith (evolvable) | [ADR-001](../decisions/ADR-001-modular-monolith.md) |
| Multi-tenancy | Schema-per-tenant | [ADR-002](../decisions/ADR-002-multi-tenancy.md) |
| Database | PostgreSQL | [ADR-003](../decisions/ADR-003-database.md) |
| Auth provider | Keycloak | [ADR-004](../decisions/ADR-004-auth-provider.md) |
| API Gateway | Apache APISIX | [ADR-005](../decisions/ADR-005-api-gateway.md) |
| Event bus | Kafka via Dapr | [ADR-006](../decisions/ADR-006-event-bus.md) |
| Frontend | React (Admin) + Next.js (Portal) | [ADR-007](../decisions/ADR-007-frontend.md) |
| Object storage | MinIO | [ADR-008](../decisions/ADR-008-object-storage.md) |
