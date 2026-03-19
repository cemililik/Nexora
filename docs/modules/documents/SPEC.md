# Module: Document Management

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
        string module_name "nullable - donations, education, ..."
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
  - Max file size: 50MB (configurable)
  - Allowed types: configurable per organization
  - Virus scanning on upload (ClamAV integration)
  - Version control: re-upload creates new version, old versions retained

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
  2. Provide variables (student name, tuition amount, date)
  3. System renders document (HTML → PDF or DOCX merge)
  4. Document saved to target folder
  5. Optionally sent for signature
- **Business Rules**:
  - Templates support merge fields: `{{student.name}}`, `{{tuition.amount}}`
  - PDF rendering via wkhtmltopdf or Puppeteer
  - Auto-generation triggered by events (e.g., enrollment accepted → generate contract)

## API Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/documents/documents` | Upload document | `documents.documents.upload` |
| GET | `/api/v1/documents/documents` | List/search documents | `documents.documents.read` |
| GET | `/api/v1/documents/documents/{id}` | Get metadata | `documents.documents.read` |
| GET | `/api/v1/documents/documents/{id}/download` | Download file | `documents.documents.read` |
| DELETE | `/api/v1/documents/documents/{id}` | Archive | `documents.documents.delete` |
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
| `donations.donation.confirmed` | Donations | Archive receipt PDF to donor's folder |
| `hr.contract.created` | HR | Generate employment contract template |

## Non-Functional Requirements

| Requirement | Target |
|------------|--------|
| Upload throughput | 100MB/s |
| Max file size | 50MB (configurable) |
| Storage per tenant | Unlimited (billed) |
| Signature page load | < 2 seconds |
| PDF generation | < 10 seconds |
| Max document versions | 100 per document |
