# Suite 09 — Cross-Module & Regression Tests

**Prerequisite:** All suites 01–08 passed
**Execution Order:** 9 (final suite)

---

## 9.1 Multi-Tenancy Isolation

- [ ] **TC-REG-001** — Tenant A cannot see Tenant B data
  - If second tenant exists: authenticate as Tenant B user
  - Query contacts, documents, reports
  - Verify: only Tenant B data returned, no Tenant A data visible

- [ ] **TC-REG-002** — Organization isolation within tenant
  - Create contact in Org A, authenticate with Org B context
  - Verify: contact not visible in Org B queries

- [ ] **TC-REG-003** — Missing tenant claim returns 401
  - Send request with JWT that has no `tenant_id` claim
  - Expected: 401 from TenantMiddleware

## 9.2 Cross-Module Data Flow

- [ ] **TC-REG-004** — Contact activity timeline includes cross-module events
  - Create contact → upload document linked to contact → send notification to contact
  - `GET /contacts/{id}/360`
  - Verify: timeline includes document and notification activities

- [ ] **TC-REG-005** — Report SQL can query contact data
  - Create report: `SELECT "FirstName", "Email" FROM contacts_contacts LIMIT 5`
  - Execute report
  - Verify: report contains contact data from Contacts module tables

- [ ] **TC-REG-006** — Report SQL can query cross-module tables
  - Create report: `SELECT c."FirstName", COUNT(d."Id") as doc_count FROM contacts_contacts c LEFT JOIN documents_documents d ON c."Id"::text = d."LinkedEntityId"::text GROUP BY c."FirstName"`
  - Execute and verify: cross-module join works within tenant schema

- [ ] **TC-REG-007** — Document linked to contact visible in contact 360
  - Upload document with `entityId = contactId, entityType = "Contact"`
  - Get contact 360 view
  - Verify: document reference appears in contact's linked documents

## 9.3 State Transition Consistency

- [ ] **TC-REG-008** — Contact archive → restore → re-archive works
  - Archive contact → verify archived
  - Restore contact → verify active
  - Archive again → verify archived
  - No errors at any step

- [ ] **TC-REG-009** — Report execution state machine
  - Execute report → status Queued → Running → Completed
  - Verify: each state transition logged correctly
  - Verify: Completed report has storageKey, rowCount, durationMs

- [ ] **TC-REG-010** — Failed report execution has error details
  - Create report with invalid SQL: `SELECT * FROM nonexistent_table_xyz`
  - Execute report → status should be Failed
  - Verify: `errorDetails` field contains meaningful error message

## 9.4 API Response Consistency

- [ ] **TC-REG-011** — All API responses use ApiEnvelope format
  - Sample 5+ different endpoints (GET, POST, PUT, DELETE)
  - Verify: all responses have `{ success, data, message, errors }` structure

- [ ] **TC-REG-012** — Validation errors return structured format
  - Send invalid data to 3+ different POST endpoints
  - Verify: 400 response with `errors` array containing field-level errors

- [ ] **TC-REG-013** — Not found returns 404 with message
  - `GET /contacts/{nonexistent-uuid}`
  - `GET /reporting/definitions/{nonexistent-uuid}`
  - Verify: 404 with `lockey_*` error message

- [ ] **TC-REG-014** — Error responses include TraceId
  - Trigger a 500 or 400 error
  - Verify: response contains `traceId` field
  - Verify: traceId can be found in Grafana Tempo

## 9.5 Performance & Stability

- [ ] **TC-REG-015** — Concurrent API requests don't cause errors
  - Send 10 simultaneous `GET /contacts` requests
  - Verify: all return 200, no 500 errors

- [ ] **TC-REG-016** — Large result set pagination works
  - Create 25+ contacts, query `GET /contacts?page=1&pageSize=10`
  - Verify: `totalCount > 20`, `totalPages > 2`, `hasNextPage = true`
  - Navigate to page 2, verify different results

- [ ] **TC-REG-017** — Report execution with large result set
  - Create report querying all contacts (no LIMIT in definition)
  - Execute report, verify it completes within 30 seconds

## 9.6 Localization

- [ ] **TC-REG-018** — Admin UI all keys resolve (no raw lockey_ visible)
  - Navigate through all admin pages
  - Verify: no raw `lockey_*` keys visible in UI (all translated)

- [ ] **TC-REG-019** — Portal UI all keys resolve
  - Navigate portal in both EN and TR
  - Verify: no raw `lockey_*` keys visible

## 9.7 MinIO & File Storage

- [ ] **TC-REG-020** — MinIO bucket auto-creation on first use
  - If no tenant bucket exists: upload a document
  - Verify: `nexora-{tenantId}` bucket auto-created in MinIO
  - Verify: report execution creates `nexora-reports` bucket if not exists
