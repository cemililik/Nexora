# Module Dependencies

## Required Dependencies

Solid arrows (`-->`) indicate **required** module dependencies. A module cannot be installed without its required dependencies.

```mermaid
---
title: Nexora Module Dependency Graph (Required)
---
flowchart TB
    subgraph Core["Core Platform (Phase 0-1)"]
        Identity["Identity & Access\nManagement"]
        Contacts["Contact\nManagement"]
        Notifications["Notification\nEngine"]
        Documents["Document\nManagement"]
        Reporting["Reporting\nEngine"]
        Portal["Portal\nFramework"]
    end

    subgraph Phase2["Phase 2 — Core Business"]
        CRM["CRM"]
        Finance["Finance"]
        Subscription["Subscription\n& Billing"]
        Projects["Project\nManagement"]
    end

    subgraph Phase3["Phase 3 — Growth"]
        CMS["Website &\nCMS"]
        Events["Event\nManagement"]
        Surveys["Survey &\nFeedback"]
        HR["HR &\nPayroll"]
        Inventory["Inventory &\nAssets"]
    end

    subgraph Phase4["Phase 4 — Advanced & Verticals"]
        Accounting["Accounting"]
        POS["Point of\nSale"]
        Fleet["Fleet\nManagement"]
        Fundraising["Fundraising\n(Vertical: NGO)"]
        Sponsorship["Sponsorship &\nPrograms\n(Vertical: NGO)"]
        Education["Education\nManagement\n(Vertical: Education)"]
    end

    %% Core internal dependencies
    Contacts --> Identity
    Notifications --> Identity
    Notifications --> Contacts
    Documents --> Identity
    Reporting --> Identity
    Portal --> Identity

    %% Phase 2 required dependencies
    CRM --> Contacts
    CRM --> Notifications
    Finance --> Contacts
    Finance --> Notifications
    Subscription --> Contacts
    Subscription --> Notifications
    Projects --> Contacts
    Projects --> Notifications

    %% Phase 3 required dependencies
    CMS --> Notifications
    Events --> Contacts
    Events --> Notifications
    Surveys --> Contacts
    Surveys --> Notifications
    HR --> Contacts
    HR --> Notifications
    HR --> Documents
    Inventory --> Contacts
    Inventory --> Notifications

    %% Phase 4 required dependencies
    Accounting --> Contacts
    Accounting --> Finance
    POS --> Contacts
    POS --> Notifications
    Fleet --> Contacts
    Fleet --> Notifications
    Fleet --> Documents
    Fundraising --> Contacts
    Fundraising --> Notifications
    Fundraising --> Documents
    Sponsorship --> Contacts
    Sponsorship --> Fundraising
    Sponsorship --> Notifications
    Education --> CRM
    Education --> Contacts
    Education --> Documents
    Education --> Notifications

    %% Styling
    style Identity fill:#e74c3c,color:#fff
    style Contacts fill:#e74c3c,color:#fff
    style Notifications fill:#e74c3c,color:#fff
    style Documents fill:#e74c3c,color:#fff
    style Reporting fill:#e74c3c,color:#fff
    style Portal fill:#e74c3c,color:#fff

    style CRM fill:#3498db,color:#fff
    style Finance fill:#3498db,color:#fff
    style Subscription fill:#3498db,color:#fff
    style Projects fill:#3498db,color:#fff

    style CMS fill:#27ae60,color:#fff
    style Events fill:#27ae60,color:#fff
    style Surveys fill:#27ae60,color:#fff
    style HR fill:#27ae60,color:#fff
    style Inventory fill:#27ae60,color:#fff

    style Accounting fill:#f39c12,color:#fff
    style POS fill:#f39c12,color:#fff
    style Fleet fill:#f39c12,color:#fff
    style Fundraising fill:#f39c12,color:#fff
    style Sponsorship fill:#f39c12,color:#fff
    style Education fill:#f39c12,color:#fff
```

> **Note**: All business modules implicitly depend on **Identity** for authentication, tenant resolution, and RBAC. These arrows are omitted from the diagram to reduce clutter. Only non-Identity required dependencies are shown as solid arrows.

## Optional Dependencies

Dashed arrows indicate **optional** dependencies. The module works without them but gains additional features when they are installed.

```mermaid
---
title: Nexora Optional Module Dependencies
---
flowchart TB
    subgraph Core["Core"]
        Documents["Documents"]
        Reporting["Reporting"]
        Portal["Portal"]
        Contacts2["Contacts"]
    end

    subgraph Phase2["Phase 2 — Core Business"]
        CRM["CRM"]
        Finance["Finance"]
        Subscription["Subscription"]
        Projects["Projects"]
    end

    subgraph Phase3["Phase 3 — Growth"]
        CMS["CMS"]
        Events["Events"]
        HR["HR"]
        Inventory["Inventory"]
    end

    subgraph Phase4["Phase 4 — Advanced & Verticals"]
        Accounting["Accounting"]
        POS["POS"]
        Fundraising["Fundraising"]
        Sponsorship["Sponsorship"]
        Education["Education"]
    end

    %% Phase 2 optional dependencies
    Subscription -.->|"dashboards"| Reporting
    Subscription -.->|"invoice attachments"| Documents
    Finance -.->|"financial reports"| Documents
    Finance -.->|"dashboards"| Reporting
    Projects -.->|"contract storage"| Documents
    Projects -.->|"cost journals"| Accounting

    %% Phase 3 optional dependencies
    Events -.->|"flyers, galleries"| Documents
    Events -.->|"follow-up workflows"| CRM
    Events -.->|"pledge tracking"| Fundraising
    CMS -.->|"form → contact"| Contacts2
    CMS -.->|"form → lead"| CRM
    CMS -.->|"donation page"| Fundraising
    CMS -.->|"public pages"| Portal
    Inventory -.->|"asset documents"| Documents

    %% Phase 4 optional dependencies
    Accounting -.->|"payroll journals"| HR
    Accounting -.->|"receipt storage"| Documents
    Accounting -.->|"alerts"| Notifications
    POS -.->|"stock deduction"| Inventory
    POS -.->|"sales journals"| Accounting
    Fundraising -.->|"lead tracking"| CRM
    Sponsorship -.->|"contract storage"| Documents
    Education -.->|"tuition billing"| Subscription

    style CRM fill:#3498db,color:#fff
    style Finance fill:#3498db,color:#fff
    style Subscription fill:#3498db,color:#fff
    style Projects fill:#3498db,color:#fff
    style CMS fill:#27ae60,color:#fff
    style Events fill:#27ae60,color:#fff
    style HR fill:#27ae60,color:#fff
    style Inventory fill:#27ae60,color:#fff
    style Accounting fill:#f39c12,color:#fff
    style POS fill:#f39c12,color:#fff
    style Fundraising fill:#f39c12,color:#fff
    style Sponsorship fill:#f39c12,color:#fff
    style Education fill:#f39c12,color:#fff
```

## Dependency Rules

1. **Core modules have no business module dependencies** — they only depend on each other
2. **All business modules depend on Identity** (implicit, not shown in diagram)
3. **Required dependencies** must be installed before the dependent module can be installed
4. **Optional dependencies** enable additional features when present (graceful degradation when absent)
5. **Cross-module communication** is always via Kafka integration events or SharedKernel interfaces, never direct references
6. **Modules within the same phase** may share domain events but never database tables

## Complete Dependency Matrix

| Module | Phase | Required Dependencies | Optional Dependencies |
|--------|-------|---------------------|----------------------|
| Identity & Access | Core | — | Keycloak, Redis |
| Contact Management | Core | identity | — |
| Notification Engine | Core | identity, contacts | — |
| Document Management | Core | identity | — |
| Reporting Engine | Core | identity | — |
| Portal Framework | Core | identity | — |
| CRM | Phase 2 | contacts, notifications | — |
| Finance | Phase 2 | contacts, notifications | documents, reporting |
| Subscription & Billing | Phase 2 | contacts, notifications | reporting, documents |
| Project Management | Phase 2 | contacts, notifications | documents, accounting |
| Website & CMS | Phase 3 | notifications | contacts, crm, fundraising, portal |
| Event Management | Phase 3 | contacts, notifications | documents, crm, fundraising |
| Surveys & Feedback | Phase 3 | contacts, notifications | — |
| HR & Payroll | Phase 3 | contacts, notifications, documents | — |
| Inventory & Assets | Phase 3 | contacts, notifications | documents |
| Accounting | Phase 4 | contacts, finance | hr, documents, notifications |
| Point of Sale | Phase 4 | contacts, notifications | inventory, accounting |
| Fleet Management | Phase 4 | contacts, notifications, documents | — |
| Fundraising | Phase 4 | contacts, notifications, documents | crm |
| Sponsorship & Programs | Phase 4 | contacts, fundraising, notifications | documents |
| Education Management | Phase 4 | crm, contacts, documents, notifications | subscription |

## Install Order (Minimum Viable)

For a General SMB:
```
Identity → Contacts → Notifications → Documents → CRM → Finance → Projects → Subscription
```

For a Growing SMB:
```
... → Events → HR → Inventory → CMS → Surveys
```

For an NGO:
```
... → CRM → Finance → Fundraising → Sponsorship → Events
```

For a School:
```
... → CRM → Finance → Education → Subscription
```

For Full Operations:
```
... → Accounting → POS → Fleet
```

## Uninstall Protection

A module **cannot be uninstalled** if another installed module has it as a **required dependency**.

```mermaid
---
title: Uninstall Dependency Check
---
flowchart LR
    Request["Uninstall: Contacts"] --> Check{"Any module\nrequires Contacts?"}
    Check -->|"Yes: CRM, Finance,\nHR, Events, ..."| Blocked["❌ Blocked\nlockey_error_cannot_uninstall_has_dependents"]
    Check -->|"No dependents\ninstalled"| Proceed["✅ Proceed\nwith uninstall"]

    style Blocked fill:#e74c3c,color:#fff
    style Proceed fill:#27ae60,color:#fff
```

### Core Module Protection
The following modules **cannot be uninstalled** regardless of dependencies:
- **Identity & Access** — foundational for all operations
- **Contact Management** — unified contact registry used by almost every module
