# Suite 05 — Document Management

**Prerequisite:** Suite 03 passed (contacts exist for entity linking)
**Execution Order:** 5
**Base URL:** `http://localhost:9080/api/v1/documents`

---

## 5.1 Folders

- [ ] **TC-DOC-001** — List root folders
  - `GET /folders`
  - Expected: 200 with folder hierarchy

- [ ] **TC-DOC-002** — Create folder
  - `POST /folders` with `{ name: "Contracts", parentFolderId: null }`
  - Expected: 201 with folder object

- [ ] **TC-DOC-003** — Create subfolder
  - `POST /folders` with `{ name: "2026", parentFolderId: "{contracts-folder-id}" }`
  - Expected: 201 with path "Contracts/2026"

- [ ] **TC-DOC-004** — Rename folder
  - `PUT /folders/{id}` with `{ name: "Agreements" }`
  - Expected: 200

- [ ] **TC-DOC-005** — Delete empty folder
  - Create temp folder, then `DELETE /folders/{id}`
  - Expected: 200 or 204

- [ ] **TC-DOC-006** — Delete system folder fails
  - Attempt to delete root/system folder
  - Expected: 400 — cannot delete system folder

## 5.2 Document Upload & CRUD

- [ ] **TC-DOC-007** — Get presigned upload URL
  - `POST /documents/upload-url` with `{ fileName: "contract.pdf", contentType: "application/pdf", folderId: "{folder-id}" }`
  - Expected: 200 with presigned URL and storageKey

- [ ] **TC-DOC-008** — Upload file to MinIO via presigned URL
  - Use the URL from TC-DOC-007 to PUT a file directly to MinIO
  - Expected: 200 from MinIO

- [ ] **TC-DOC-009** — Confirm upload
  - `POST /documents/confirm-upload` with `{ storageKey: "{key}", name: "contract.pdf", folderId: "{folder-id}" }`
  - Expected: 201 with document object
  - Verify: document appears in MinIO bucket

- [ ] **TC-DOC-010** — List documents
  - `GET /documents?page=1&pageSize=10`
  - Expected: 200 with at least 1 document

- [ ] **TC-DOC-011** — Get document by ID
  - `GET /documents/{id}`
  - Expected: 200 with full document details (name, size, mime, folder)

- [ ] **TC-DOC-012** — Download document (presigned URL)
  - `GET /documents/{id}/download`
  - Expected: 200 with presigned download URL
  - Verify: URL is accessible and returns file content

- [ ] **TC-DOC-013** — Update document metadata
  - `PUT /documents/{id}` with `{ name: "Updated Contract.pdf", description: "Q1 2026 contract" }`
  - Expected: 200

- [ ] **TC-DOC-014** — Move document to different folder
  - `POST /documents/{id}/move` with `{ targetFolderId: "{subfolder-id}" }`
  - Expected: 200

- [ ] **TC-DOC-015** — Link document to contact
  - `POST /documents/{id}/link` with `{ entityId: "{contact-id}", entityType: "Contact" }`
  - Expected: 200

- [ ] **TC-DOC-016** — Unlink document from contact
  - `DELETE /documents/{id}/link`
  - Expected: 200

- [ ] **TC-DOC-017** — Archive document
  - `DELETE /documents/{id}`
  - Expected: 200, status = Archived

- [ ] **TC-DOC-018** — Restore archived document
  - `POST /documents/{id}/restore`
  - Expected: 200, status = Active

## 5.3 Document Versions

- [ ] **TC-DOC-019** — Add new version
  - `POST /documents/{documentId}/versions` with new file data
  - Expected: 201 with version number incremented

- [ ] **TC-DOC-020** — List versions
  - `GET /documents/{documentId}/versions`
  - Expected: 200 with version history (at least 2 versions)

## 5.4 Document Access Control

- [ ] **TC-DOC-021** — Grant access to user
  - `POST /documents/{documentId}/access` with `{ userId: "{user-id}", level: "View" }`
  - Expected: 201

- [ ] **TC-DOC-022** — List access grants
  - `GET /documents/{documentId}/access`
  - Expected: 200 with access list

- [ ] **TC-DOC-023** — Revoke access
  - `DELETE /documents/{documentId}/access/{accessId}`
  - Expected: 200 or 204

## 5.5 Digital Signatures

- [ ] **TC-DOC-024** — Create signature request
  - `POST /signatures` with `{ documentId: "{doc-id}", recipients: [{ email: "signer@test.com", name: "Signer" }] }`
  - Expected: 201 with signature request in Draft status

- [ ] **TC-DOC-025** — Send signature request
  - `POST /signatures/{id}/send`
  - Expected: 200, status = Sent

- [ ] **TC-DOC-026** — Sign document
  - `POST /signatures/{id}/sign` with signature data
  - Expected: 200

- [ ] **TC-DOC-027** — Decline signature
  - Create new request and: `POST /signatures/{id}/decline` with `{ reason: "Terms unacceptable" }`
  - Expected: 200

## 5.6 Document Templates

- [ ] **TC-DOC-028** — Create document template
  - `POST /templates` with `{ name: "Invoice Template", category: "Finance", body: "Invoice for {{customer_name}}", format: "PDF", variables: ["customer_name", "amount"] }`
  - Expected: 201
  - Test activate, render, and deactivate lifecycle
