# Suite 06 — Reporting Engine

**Prerequisite:** Suite 03 passed (data exists for SQL queries)
**Execution Order:** 6
**Base URL:** `http://localhost:9080/api/v1/reporting`

---

## 6.1 Report Definition CRUD

- [ ] **TC-RPT-001** — Create report definition
  - `POST /definitions` with `{ name: "Contact List", module: "contacts", queryText: "SELECT \"Id\", \"FirstName\", \"LastName\", \"Email\" FROM contacts_contacts", defaultFormat: "Pdf" }`
  - Expected: 201 with definition object

- [ ] **TC-RPT-002** — Create definition with DML query fails
  - `POST /definitions` with `{ queryText: "DELETE FROM contacts_contacts" }`
  - Expected: 400 — SQL validation rejects DML

- [ ] **TC-RPT-003** — Create definition with semicolon fails
  - `POST /definitions` with `{ queryText: "SELECT 1; DROP TABLE contacts_contacts" }`
  - Expected: 400 — semicolons not allowed

- [ ] **TC-RPT-004** — Create definition starting with non-SELECT fails
  - `POST /definitions` with `{ queryText: "INSERT INTO contacts_contacts VALUES (1)" }`
  - Expected: 400 — must start with SELECT or WITH

- [ ] **TC-RPT-005** — List definitions
  - `GET /definitions?page=1&pageSize=10`
  - Expected: 200 with at least 1 definition

- [ ] **TC-RPT-006** — Get definition by ID
  - `GET /definitions/{id}`
  - Expected: 200 with full details (query, format, parameters)

- [ ] **TC-RPT-007** — Update definition
  - `PUT /definitions/{id}` with updated name and query
  - Expected: 200

- [ ] **TC-RPT-008** — Update definition with invalid SQL fails
  - `PUT /definitions/{id}` with `{ queryText: "UPDATE contacts_contacts SET ..." }`
  - Expected: 400 — SQL validation

- [ ] **TC-RPT-009** — Delete definition
  - Create temp definition, then `DELETE /definitions/{id}`
  - Expected: 204 No Content

## 6.2 Test Query

- [ ] **TC-RPT-010** — Test valid query
  - `POST /definitions/test-query` with `{ queryText: "SELECT \"Id\", \"FirstName\" FROM contacts_contacts" }`
  - Expected: 200 with columns and rows (max 10 rows)
  - Verify: response contains `columns`, `rows`, `rowCount`

- [ ] **TC-RPT-011** — Test invalid query
  - `POST /definitions/test-query` with `{ queryText: "SELECT * FROM nonexistent_table" }`
  - Expected: 400 with error message

- [ ] **TC-RPT-012** — Test DML in test query fails
  - `POST /definitions/test-query` with `{ queryText: "DROP TABLE contacts_contacts" }`
  - Expected: 400 — SQL validation rejects

## 6.3 Report Execution

- [ ] **TC-RPT-013** — Execute report (PDF)
  - `POST /executions` with `{ definitionId: "{id}", format: "Pdf" }`
  - Expected: 201 with execution object, status = Queued
  - Wait 5 seconds, then check status

- [ ] **TC-RPT-014** — Execution completes successfully
  - `GET /executions/{id}` (poll until status changes)
  - Expected: status = Completed, rowCount > 0, durationMs > 0

- [ ] **TC-RPT-015** — Download completed report (presigned URL)
  - `GET /executions/{id}/download`
  - Expected: 200 with `{ url, expiresAt }`

- [ ] **TC-RPT-016** — Download completed report (file stream)
  - `GET /executions/{id}/file`
  - Expected: 200 with binary PDF content, Content-Type: application/pdf

- [ ] **TC-RPT-017** — Execute report (CSV format)
  - `POST /executions` with `{ definitionId: "{id}", format: "Csv" }`
  - Wait for completion, then `GET /executions/{id}/file`
  - Expected: Content-Type: text/csv

- [ ] **TC-RPT-018** — Execute report (Excel format)
  - `POST /executions` with `{ definitionId: "{id}", format: "Excel" }`
  - Wait for completion, verify file download works

- [ ] **TC-RPT-019** — List executions
  - `GET /executions?definitionId={id}&page=1&pageSize=10`
  - Expected: 200 with multiple executions from previous tests

- [ ] **TC-RPT-020** — MinIO bucket contains report files
  - Check MinIO console or `mc ls local/nexora-reports/`
  - Expected: report files exist under `reports/{tenantId}/`

## 6.4 Report Schedules

- [ ] **TC-RPT-021** — Create schedule
  - `POST /schedules` with `{ definitionId: "{id}", cronExpression: "0 9 * * MON", format: "Pdf", recipients: "[\"admin@nexora.dev\"]" }`
  - Expected: 201 with schedule object

- [ ] **TC-RPT-022** — List schedules
  - `GET /schedules?definitionId={id}`
  - Expected: 200 with at least 1 schedule

- [ ] **TC-RPT-023** — Delete schedule
  - `DELETE /schedules/{id}`
  - Expected: 204

## 6.5 Dashboards

- [ ] **TC-RPT-024** — Create dashboard
  - `POST /dashboards` with `{ name: "Operations Dashboard", isDefault: true }`
  - Expected: 201
  - Verify: list, get by ID, update, delete lifecycle works
