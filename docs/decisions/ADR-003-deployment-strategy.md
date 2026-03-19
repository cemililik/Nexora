# ADR-003: Deployment Strategy

## Status
**Accepted** — 2026-03-19

## Context

Nexora is an enterprise modular platform designed to serve multiple organizations. We need a clear deployment strategy that answers:

1. **Where does Nexora run?** — Our infrastructure, customer infrastructure, or both?
2. **How are new tenants provisioned?** — Manual, semi-automated, or fully automated?
3. **How is the software delivered?** — Docker images, Helm charts, binaries?
4. **How do we handle updates?** — Rolling updates, blue-green, per-tenant versioning?
5. **What's the licensing model?** — Per-tenant, per-user, per-module?

The platform must support organizations ranging from small NGOs (10 users) to large educational institutions (500+ users), with varying requirements for data sovereignty, compliance, and operational control.

## Decision

We will support **three deployment models**, all powered by the same codebase and Helm chart:

### Model 1: Nexora Cloud (Multi-Tenant SaaS)
**Target**: Organizations that want zero infrastructure management.

```mermaid
---
title: Nexora Cloud Architecture
---
flowchart TB
    subgraph Internet
        TenantA["Tenant A\n(Isabet Academy)\nisabet.nexora.app"]
        TenantB["Tenant B\n(Another NGO)\nngo.nexora.app"]
        TenantC["Tenant C\n(School)\nokul.nexora.app"]
    end

    subgraph NexoraCloud["Nexora Cloud (Our Infrastructure)"]
        subgraph Gateway["Edge Layer"]
            DNS["DNS\n(Wildcard *.nexora.app)"]
            CDN["CDN\n(Cloudflare/AWS CloudFront)"]
            LB["Load Balancer\n(APISIX)"]
        end

        subgraph AppTier["Application Tier"]
            API1["Nexora API\nReplica 1"]
            API2["Nexora API\nReplica 2"]
            API3["Nexora API\nReplica 3"]
            Worker["Background Workers\n(Hangfire)"]
        end

        subgraph DataTier["Data Tier"]
            PG[("PostgreSQL HA\nschema-per-tenant")]
            Redis[("Redis Cluster\ncache & sessions")]
            Kafka[("Kafka\nevents")]
            MinIO[("MinIO\nfile storage")]
        end

        subgraph Auth["Identity"]
            KC["Keycloak\nrealm-per-tenant"]
        end
    end

    TenantA --> DNS
    TenantB --> DNS
    TenantC --> DNS
    DNS --> CDN --> LB
    LB --> API1
    LB --> API2
    LB --> API3
    API1 & API2 & API3 --> PG
    API1 & API2 & API3 --> Redis
    API1 & API2 & API3 --> Kafka
    API1 & API2 & API3 --> MinIO
    API1 & API2 & API3 --> KC
    Worker --> PG
    Worker --> Kafka
```

**Characteristics**:
- Single Kubernetes cluster, shared infrastructure
- Schema-per-tenant isolation in PostgreSQL
- Realm-per-tenant isolation in Keycloak
- Bucket-per-tenant in MinIO
- Custom subdomain per tenant (`{slug}.nexora.app`) or custom domain (CNAME)
- We manage everything: updates, backups, scaling, monitoring
- **Pricing**: Per-tenant base fee + per-active-user fee + module add-ons

### Model 2: Nexora Dedicated (Single-Tenant Managed)
**Target**: Large organizations that need dedicated resources or data residency compliance.

```mermaid
---
title: Nexora Dedicated Architecture
---
flowchart TB
    subgraph CustomerRegion["Customer's Preferred Region"]
        subgraph Dedicated["Dedicated Namespace / Cluster"]
            DAPI["Nexora API\n(dedicated pods)"]
            DPG[("PostgreSQL\n(dedicated instance)")]
            DKC["Keycloak\n(dedicated realm)"]
            DMinIO[("MinIO\n(dedicated bucket)")]
        end
    end

    subgraph NexoraOps["Nexora Operations"]
        Monitor["Monitoring\n& Alerting"]
        Updates["Update\nPipeline"]
        Backup["Backup\nService"]
    end

    NexoraOps -.->|"manages"| Dedicated
    Monitor -.->|"observes"| DAPI
    Updates -.->|"deploys"| DAPI
    Backup -.->|"snapshots"| DPG
```

**Characteristics**:
- Dedicated Kubernetes namespace (or cluster for largest customers)
- Same Helm chart, same container images
- Data stays in customer's chosen region (EU, US, TR, etc.)
- We still manage operations, but customer has audit access
- Can use customer's own cloud account (AWS, Azure, GCP)
- **Pricing**: Dedicated infrastructure fee + platform license

### Model 3: Nexora Self-Hosted (On-Premise)
**Target**: Organizations with strict data sovereignty requirements (government, military, healthcare).

```mermaid
---
title: Nexora Self-Hosted Architecture
---
flowchart TB
    subgraph CustomerDC["Customer Data Center / Private Cloud"]
        subgraph K8s["Kubernetes (customer-managed)"]
            API["Nexora API"]
            Worker["Workers"]
            KC["Keycloak"]
        end
        PG[("PostgreSQL")]
        Redis[("Redis")]
        Kafka[("Kafka")]
        MinIO[("MinIO")]

        API --> PG
        API --> Redis
        API --> Kafka
        API --> MinIO
        API --> KC
        Worker --> PG
        Worker --> Kafka
    end

    subgraph NexoraLicense["Nexora License Server"]
        License["License Validation\n(online or air-gapped)"]
    end

    API -.->|"license check\n(periodic)"| License

    subgraph NexoraSupport["Nexora Support (Optional)"]
        RemoteSupport["Remote Support\n(VPN tunnel)"]
        UpdateRepo["Helm Chart\nRegistry"]
    end

    K8s -.->|"helm pull"| UpdateRepo
    RemoteSupport -.->|"troubleshoot"| K8s
```

**Characteristics**:
- Customer installs via Helm chart on their own Kubernetes cluster
- All data stays on customer premises — no data leaves their network
- License key controls which modules are activated
- Air-gapped mode supported (offline license validation)
- Customer manages operations (with our documentation + optional support contract)
- Updates delivered as new Helm chart versions — customer controls upgrade timing
- **Pricing**: Annual license fee + optional support contract

---

## Deployment Model Comparison

| Feature | Cloud (SaaS) | Dedicated | Self-Hosted |
|---------|-------------|-----------|-------------|
| Infrastructure | Nexora-managed | Nexora-managed, customer region | Customer-managed |
| Data Location | Nexora cloud (multi-region) | Customer's chosen region | Customer premises |
| Multi-Tenancy | Shared (schema isolation) | Single-tenant | Single or multi-tenant |
| Updates | Automatic (rolling) | Managed (scheduled window) | Customer-controlled |
| Backups | Included | Included | Customer responsibility |
| SLA | 99.9% | 99.95% | Depends on customer |
| Monitoring | Included (Grafana) | Included + customer access | Customer responsibility |
| Custom Domain | Supported (CNAME) | Supported | N/A (own domain) |
| Min. Setup Time | Minutes | 1-2 days | 1 week (with support) |
| Ideal For | SMB, startups, NGOs | Enterprise, regulated | Government, air-gapped |
| Pricing Model | Per-user + per-module | Infrastructure + license | Annual license |

---

## Licensing Model

### Module-Based Licensing

```mermaid
---
title: Nexora Licensing Structure
---
flowchart TB
    subgraph Platform["Platform License (Always Included)"]
        Identity["Identity & Access"]
        Contacts["Contact Management"]
        Notifications["Notification Engine"]
        Documents["Document Management"]
    end

    subgraph AddOns["Module Add-Ons (Per Module Pricing)"]
        CRM["CRM"]
        Donations["Donations"]
        Sponsorship["Sponsorship"]
        Events["Events"]
        Education["Education"]
        Subscription["Subscription"]
        CMS["Website & CMS"]
        Surveys["Surveys"]
        Accounting["Accounting"]
        HR["HR & Payroll"]
        POS["Point of Sale"]
        Fleet["Fleet"]
        Inventory["Inventory"]
        Projects["Projects"]
    end

    Platform --> AddOns

    style Platform fill:#27ae60,color:#fff
    style AddOns fill:#3498db,color:#fff
```

### License Key Structure

```json
{
  "licenseId": "LIC-2026-00042",
  "tenantId": "tenant_isabet",
  "type": "cloud|dedicated|self-hosted",
  "plan": "enterprise",
  "validUntil": "2027-03-19T00:00:00Z",
  "maxUsers": 200,
  "maxOrganizations": 5,
  "modules": [
    "crm", "donations", "sponsorship", "events",
    "education", "subscription", "documents"
  ],
  "features": {
    "customDomain": true,
    "whiteLabel": true,
    "apiAccess": true,
    "ssoIntegration": true
  },
  "signature": "base64-encoded-rsa-signature"
}
```

### Pricing Tiers

| Tier | Core Modules | Add-On Modules | Users | Orgs | Support |
|------|-------------|----------------|-------|------|---------|
| **Starter** | All 4 core | Up to 2 | 25 | 1 | Email |
| **Professional** | All 4 core | Up to 6 | 100 | 3 | Email + Chat |
| **Enterprise** | All 4 core | Unlimited | Unlimited | Unlimited | Priority + SLA |

---

## Update Strategy

### Cloud (SaaS) — Zero-Downtime Rolling Updates

```mermaid
---
title: SaaS Rolling Update
---
sequenceDiagram
    participant CI as GitHub Actions
    participant Registry as Container Registry
    participant K8s as Kubernetes
    participant Old as Old Pods (v1.2.0)
    participant New as New Pods (v1.3.0)
    participant DB as PostgreSQL

    CI->>Registry: Push nexora:v1.3.0
    CI->>K8s: kubectl set image (rolling)
    K8s->>New: Start pod v1.3.0 (1 of 3)
    New->>DB: Run pending migrations
    Note over New,DB: Migrations are additive-only<br/>(no breaking changes)
    New-->>K8s: Health check OK
    K8s->>Old: Terminate old pod (1 of 3)
    K8s->>New: Start pod v1.3.0 (2 of 3)
    Note over K8s: Repeat until all pods updated
    K8s-->>CI: Rollout complete
```

**Rules**:
- Database migrations are **additive-only** (add column, add table — never drop/rename in production)
- Breaking schema changes use **expand-contract pattern** (add new → migrate data → remove old in next release)
- Canary deployments for major releases (route 10% traffic to new version first)

### Self-Hosted — Customer-Controlled Updates

```bash
# Customer updates at their own pace
helm repo update nexora
helm upgrade nexora nexora/nexora-platform \
  --version 1.3.0 \
  --values my-values.yaml \
  --namespace nexora
```

- Customers get notification of new versions via admin panel
- Release notes highlight breaking changes and migration steps
- Minimum supported version policy: N-2 (current and two previous major versions)

---

## Consequences

### Positive
- **Single codebase** serves all deployment models — no fork maintenance
- **Helm chart** as universal delivery mechanism — works on any Kubernetes
- **Schema-per-tenant** scales to thousands of tenants on shared infrastructure
- **Module licensing** creates flexible pricing for different organization sizes
- **Easy onboarding** — Cloud tenants operational in minutes

### Negative
- Must maintain backward-compatible migrations (additive-only constraint)
- Self-hosted requires documentation + support effort for customer-managed K8s
- License validation for air-gapped environments adds complexity
- Must test on multiple Kubernetes distributions (EKS, AKS, GKE, bare-metal)

### Risks
- **Data compliance**: Different countries have different data residency laws — Dedicated/Self-Hosted models mitigate this
- **Version fragmentation**: Self-hosted customers may lag behind — N-2 support policy limits this
- **Operational overhead**: Supporting three deployment models requires mature DevOps practices

## Related
- [ADR-001: Modular Monolith](./ADR-001-modular-monolith.md)
- [ADR-002: Schema-per-Tenant Multi-Tenancy](./ADR-002-multi-tenancy.md)
- [Tenant Provisioning & Operations](../operations/TENANT_OPERATIONS.md)
- [Helm Installation Guide](../operations/HELM_INSTALLATION.md)
