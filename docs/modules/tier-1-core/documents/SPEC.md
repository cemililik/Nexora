# Module: Document Management

**Tier:** 1 — Platform Core

> **Status**: Implemented
> **Module Name**: `documents`
> **Tier**: Core/Platform (always installed)
> **Dependencies**: `identity`

## Overview
The Document module provides centralized file storage, organization, digital signatures, and document templates across all Nexora modules. It handles contract signing (enrollment agreements, vendor contracts), receipt archival, official document scanning, and a knowledge base for institutional memory. Files are stored in MinIO (S3-compatible) with tenant-isolated buckets.

## Domain Model

### Entities

```mermaid
---
title: Document Module - Entity Relationship Diagram
---
erDiagram
    Folder ||--o{ Folder : "has subfolders"
    Folder ||--o{ Document : "contains"
    Folder {
        uuid id PK
        uuid organization_id FK
        uuid parent_folder_id FK "nullable"
        string name
        string path "computed: /Official/Contracts/2026"
        uuid module_ref "nullable - auto-created by module"
        string module_name "nullable - fundraising, education, ..."
        uuid owner_user_id FK
        boolean is_system "cannot delete"
    }

    Document ||--o{ DocumentVersion : "has versions"
    Document ||--o{ DocumentAccess : "shared with"
    Document {
        uuid id PK
        uuid folder_id FK
        uuid organization_id FK
        uuid uploaded_by_user_id FK
        string name
        string description
        string mime_type
        long file_size
        string storage_key "MinIO object key"
        string status "active, archived, deleted"
        uuid linked_entity_id FK "nullable - contact, student, project, ..."
        string linked_entity_type "nullable"
        int current_version
        jsonb tags
        timestamp created_at
        timestamp updated_at
    }

    DocumentVersion {
        uuid id PK
        uuid document_id FK
        int version_number
        string storage_key
        long file_size
        string change_note
        uuid uploaded_by_user_id FK
        timestamp created_at
    }

    DocumentAccess {
        uuid id PK
        uuid document_id FK
        uuid user_id FK "nullable"
        uuid role_id FK "nullable"
        string permission "view, edit, manage"
    }

    SignatureRequest ||--o{ SignatureRecipient : "sent to"
    SignatureRequest {
        uuid id PK
        uuid organization_id FK
        uuid document_id FK
        uuid created_by_user_id FK
        string title
        string status "draft, sent, partially_signed, completed, cancelled, expired"
        date expires_at
        timestamp created_at
        timestamp completed_at
    }

    SignatureRecipient {
        uuid id PK
        uuid request_id FK
        uuid contact_id FK
        string email
        string name
        int signing_order
        string status "pending, viewed, signed, declined, expired"
        string signature_data "base64 or reference"
        string ip_address
        timestamp signed_at
    }

    DocumentTemplate {
        uuid id PK
        uuid organization_id FK
        string name "Enrollment Contract, Volunteer Agreement, ..."
        string category "contract, receipt, letter, report"
        string format "docx, pdf, html"
        string template_storage_key
        jsonb variable_definitions "list of merge fields"
        boolean is_active
    }
```

### Entity Lifecycles

```mermaid
---
title: Signature Request Lifecycle
---
stateDiagram-v2
    [*] --> Draft: Create request
    Draft --> Sent: Send to recipients
    Sent --> PartiallySigned: Some signed
    Sent --> Completed: All signed (single recipient)
    PartiallySigned --> Completed: All signed
    Sent --> Expired: Expiry date reached
    Sent --> Cancelled: Creator cancels
    PartiallySigned --> Expired: Expiry date
    PartiallySigned --> Cancelled: Creator cancels
    Completed --> [*]
    Expired --> [*]
    Cancelled --> [*]

    note right of Completed: Document archived\nto signer's folder
```

### Sequence Diagrams

```mermaid
---
title: Document Upload Flow (Presigned URL)
---
sequenceDiagram
    participant User
    participant Frontend as nexora-admin
    participant API as Documents API
    participant Handler as UploadHandler
    participant MinIO
    participant DB as PostgreSQL

    User->>Frontend: Select file (drag & drop / picker)
    Frontend->>API: POST /api/v1/documents/documents {fileName, mimeType, folderId}
    API->>Handler: Send(GenerateUploadUrlCommand)
    Handler->>Handler: Validate MIME type whitelist & file size limit
    Handler->>MinIO: Generate presigned PUT URL (TTL 15min)
    MinIO-->>Handler: Presigned URL + storage key
    Handler->>DB: Create Document record (status: pending_upload)
    Handler-->>API: Result.Success({uploadUrl, documentId})
    API-->>Frontend: ApiEnvelope<UploadUrlDto>

    Frontend->>MinIO: PUT file via presigned URL (XHR direct upload)
    MinIO-->>Frontend: 200 OK

    Frontend->>API: POST /api/v1/documents/documents/{id}/confirm
    API->>Handler: Send(ConfirmUploadCommand)
    Handler->>MinIO: HEAD object (verify upload exists)
    MinIO-->>Handler: Object metadata (size, etag)
    Handler->>DB: Update Document (status: active, file_size, current_version: 1)
    Handler->>DB: Create DocumentVersion (version_number: 1)
    Handler-->>API: Result.Success(DocumentDto)
    API-->>Frontend: ApiEnvelope<DocumentDto>
```

```mermaid
---
title: Folder Access Grant Flow
---
sequenceDiagram
    participant Admin
    participant API as Documents API
    participant Handler as GrantFolderAccessHandler
    participant DB as PostgreSQL
    participant Kafka

    Admin->>API: POST /api/v1/documents/folders/{folderId}/access {userId, permission, expiresAt}
    API->>Handler: Send(GrantFolderAccessCommand)
    Handler->>DB: Verify folder exists and admin has manage permission
    Handler->>DB: Check for existing FolderAccess grant
    alt Already granted
        Handler->>DB: Update permission level & expiresAt
    else New grant
        Handler->>DB: Insert FolderAccess record
    end
    Handler->>DB: SaveChangesAsync
    Handler->>Kafka: Publish FolderAccessGranted event
    Handler-->>API: Result.Success
    API-->>Admin: ApiEnvelope (success)

    Note over Kafka: Notifications module sends<br/>access notification to user
    Note over DB: FolderAccessExpiryJob revokes<br/>expired grants automatically
```

```mermaid
---
title: Document Download Flow
---
sequenceDiagram
    participant User
    participant Frontend as nexora-admin
    participant API as Documents API
    participant Handler as DownloadHandler
    participant DB as PostgreSQL
    participant MinIO

    User->>Frontend: Click download on document
    Frontend->>API: GET /api/v1/documents/documents/{id}/download
    API->>Handler: Send(DownloadDocumentQuery)
    Handler->>DB: Load Document record
    Handler->>Handler: Check user permission (documents.documents.read)<br/>+ folder access if applicable
    alt Access denied
        Handler-->>API: Result.Failure (forbidden)
        API-->>Frontend: 403 Forbidden
    else Access granted
        Handler->>MinIO: Generate presigned GET URL (TTL 5min)
        MinIO-->>Handler: Presigned download URL
        Handler-->>API: Result.Success({downloadUrl, fileName, mimeType})
        API-->>Frontend: ApiEnvelope<DownloadUrlDto>
        Frontend->>MinIO: GET file via presigned URL
        MinIO-->>Frontend: File stream
        Frontend->>User: Browser download dialog
    end
```

### Component Diagram

```mermaid
---
title: Documents Module - Component Diagram
---
flowchart TD
    subgraph Api["Api Layer"]
        DE[DocumentEndpoints]
        FE[FolderEndpoints]
        SE[SignatureEndpoints]
        TE[TemplateEndpoints]
    end

    subgraph Application["Application Layer"]
        CMD[Commands<br/>GenerateUploadUrl, ConfirmUpload,<br/>GrantFolderAccess, CreateSignRequest, ...]
        QRY[Queries<br/>GetDocument, ListFolders,<br/>DownloadDocument, ...]
        VAL[Validators]
        SVC[Services<br/>TemplateRenderer]
    end

    subgraph Domain["Domain Layer"]
        ENT[Entities<br/>Document, Folder, DocumentVersion,<br/>DocumentAccess, SignatureRequest,<br/>SignatureRecipient, DocumentTemplate]
        VO[Value Objects<br/>DocumentId, FolderId, StorageKey]
        EVT[Domain Events<br/>DocumentUploaded, SignatureCompleted]
    end

    subgraph Infrastructure["Infrastructure Layer"]
        DBC[DocumentsDbContext]
        MINIO[MinioStorageService<br/>Presigned URLs, object ops]
        JOBS[Background Jobs<br/>FolderAccessExpiryJob]
    end

    subgraph External["External Services"]
        MS[(MinIO<br/>S3-compatible storage)]
        PG[(PostgreSQL)]
        KF[Kafka<br/>Event bus]
    end

    Api --> Application
    Application --> Domain
    Application --> Infrastructure
    Infrastructure --> External

    MINIO --> MS
    DBC --> PG
```

### Integration Diagram

```mermaid
---
title: Documents Module - Integration Diagram
---
flowchart LR
    subgraph Clients
        AdminUI[nexora-admin]
        Portal[nexora-portal]
    end

    subgraph Documents["Documents Module"]
        DAPI[Documents API]
        DApp[Application Layer]
        DInfra[Infrastructure Layer]
    end

    subgraph Storage["File Storage"]
        MinIO[(MinIO<br/>Tenant-isolated buckets)]
    end

    subgraph SharedInfra["Shared Infrastructure"]
        PG[(PostgreSQL)]
        Kafka[Kafka]
    end

    subgraph ProducerModules["Event Producers"]
        Education[Education Module]
        Fundraising[Fundraising Module]
        HR[HR Module]
    end

    subgraph Identity["Identity Module"]
        Auth[Auth & Permissions]
    end

    Clients -->|Presigned URL upload/download| MinIO
    Clients -->|API calls| DAPI
    DInfra -->|Generate presigned URLs| MinIO
    DInfra --> PG
    DApp -->|Publish events| Kafka

    Kafka -->|enrollment.accepted| DApp
    Kafka -->|fundraising.donation.confirmed| DApp
    Kafka -->|contract.created| DApp

    DAPI -->|Permission check| Auth
```

## Use Cases

### UC-DOC-001: Upload & Organize Document
- **Actor**: User with `documents.documents.upload` permission
- **Flow**:
  1. User uploads file (drag & drop or file picker)
  2. System stores in MinIO: `{tenant}/{org}/{folder_path}/{uuid}_{filename}`
  3. System creates Document record with metadata
  4. Optionally link to entity (contact, student, project)
  5. Document appears in folder and on linked entity's document tab
- **Business Rules**:
  - Max file size: 100MB (configurable, enforced server-side)
  - Allowed types: MIME type whitelist (configurable per organization)
  - Path traversal guard on file names
  - Virus scanning on upload (ClamAV integration)
  - Version control: re-upload with same name creates new version, old versions retained

### UC-DOC-002: Digital Signature (e-Sign)
- **Actor**: User with `documents.signatures.create` permission
- **Flow**:
  1. User uploads or selects document for signing
  2. User defines recipients (contacts) and signing order
  3. System sends email to first recipient with secure signing link
  4. Recipient views document, draws/types signature, submits
  5. Next recipient in order receives email (if sequential signing)
  6. When all signed: status → Completed
  7. Signed document with audit trail archived to folder
  8. All parties receive final signed copy via email
- **Business Rules**:
  - Signatures legally binding (timestamp, IP, identity verification)
  - Expiry: configurable (default 7 days)
  - Signing order: sequential or parallel
  - Reminder emails: auto-send every 2 days if unsigned

### UC-DOC-003: Generate Document from Template
- **Actor**: User or system (automated)
- **Flow**:
  1. Select template (e.g., "Enrollment Contract")
  2. Provide variables (see declarative variable table below)
  3. System renders document (HTML → PDF or DOCX merge)
  4. Document saved to target folder
  5. Optionally sent for signature
- **Template Variables** (declarative, per template definition):

  | Variable | Type | Example |
  |----------|------|---------|
  | `student.name` | string | "Ada Lovelace" |
  | `tuition` | `Money` | `{ amount: 12500, currency: "USD" }` |
  | `deposit` | `Money` | `{ amount: 1000, currency: "USD" }` |
  | `enrollment.date` | date | 2026-09-01 |

  > Monetary fields MUST be typed as `Money` (SharedKernel.Money — see ADR-0021). Do NOT split into separate `amount` + `currency` template variables; bind the whole `Money` value object and format via locale-aware renderer.

- **Business Rules**:
  - Templates support merge fields: `{{student.name}}`, `{{tuition}}` (renders formatted `Money`), `{{tuition.amount}}` and `{{tuition.currency}}` available as sub-accessors for display-only formatting
  - PDF rendering via wkhtmltopdf or Puppeteer
  - Auto-generation triggered by events (e.g., enrollment accepted → generate contract)

## API Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/documents/documents` | Upload document | `documents.documents.upload` |
| GET | `/api/v1/documents/documents` | List/search documents | `documents.documents.read` |
| GET | `/api/v1/documents/documents/{id}` | Get metadata | `documents.documents.read` |
| GET | `/api/v1/documents/documents/{id}/download` | Download file | `documents.documents.read` |
| DELETE | `/api/v1/documents/documents/{id}` | Archive (returns 200 OK with ApiEnvelope) | `documents.documents.delete` |
| GET | `/api/v1/documents/folders` | List folders | `documents.folders.read` |
| POST | `/api/v1/documents/folders` | Create folder | `documents.folders.manage` |
| POST | `/api/v1/documents/signatures` | Create sign request | `documents.signatures.create` |
| GET | `/api/v1/documents/signatures/{id}` | Get sign status | `documents.signatures.read` |
| POST | `/api/v1/documents/signatures/{id}/sign` | Sign document | Recipient token |
| POST | `/api/v1/documents/templates/{id}/render` | Render template | `documents.templates.use` |
| GET | `/api/v1/documents/templates` | List templates | `documents.templates.read` |
| POST | `/api/v1/documents/templates` | Create template | `documents.templates.manage` |

## Integration Points

### Events Produced
| Event | Topic |
|-------|-------|
| `documents.document.uploaded` | `nexora.documents` |
| `documents.document.signed` | `nexora.documents.signatures` |
| `documents.signature.completed` | `nexora.documents.signatures` |

### Events Consumed
| Event | Source | Action |
|-------|--------|--------|
| `education.enrollment.accepted` | Education | Auto-generate enrollment contract, send for signature |
| `fundraising.donation.confirmed` | Fundraising | Archive receipt PDF to donor's folder |
| `hr.contract.created` | HR | Generate employment contract template |

## Recent Enhancements (Implemented)

### Folder Access Control
- `FolderAccess` entity with temporal permissions (`ExpiresAt` nullable timestamp)
- `GrantFolderAccessCommand` / `RevokeFolderAccessCommand` for managing per-user/role folder permissions
- `GetFolderAccessQuery` to list active grants for a folder
- `FolderAccessExpiryJob` — Hangfire background job that revokes expired folder access grants automatically

### Upload UX
- `FileDropZone` frontend component (drag & drop upload area)
- 3-phase presigned URL upload flow: `GenerateUploadUrl` → client uploads to MinIO → `ConfirmUpload`
- Duplicate detection: uploading a file with the same name to the same folder automatically creates a new version instead of a duplicate document

### Document Preview
- Inline preview dialog: PDF rendered via iframe, images via `<img>` tag, other types show a download fallback

### Template Editors
- `VariableDefinitionsEditor` component — define merge field names and types for document templates
- `VariableEditor` component — fill in variable values when rendering a template

### Upload Security
- MIME type whitelist validation (only allowed content types accepted)
- 100 MB maximum file size enforcement
- Path traversal guard on storage key generation (sanitized file names)

## Non-Functional Requirements

| Requirement | Target |
|------------|--------|
| Upload throughput | 100MB/s |
| Max file size | 100MB (configurable) |
| Storage per tenant | Unlimited (billed) |
| Signature page load | < 2 seconds |
| PDF generation | < 10 seconds |
| Max document versions | 100 per document |
