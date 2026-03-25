# Suite 07 — Admin Dashboard (nexora-admin) UI Tests

**Prerequisite:** Suites 02–06 passed (backend data exists)
**Execution Order:** 7
**URL:** `http://localhost:3001`

---

## 7.1 Authentication & Navigation

- [ ] **TC-ADM-001** — Login flow
  - Navigate to `http://localhost:3001`
  - Redirected to Keycloak login
  - Enter `platformadmin@nexora.dev` / `Admin123!`
  - Verify: redirected to admin dashboard, sidebar visible

- [ ] **TC-ADM-002** — Sidebar shows all module navigation items
  - Verify sidebar contains: Kullanıcılar, Roller, Organizasyonlar, Kiracılar, Denetim Kayıtları, Kişiler, Etiketler, Özel Alanlar, İçe/Dışa Aktar, Belgeler, Klasörler, Bildirimler, Raporlar

- [ ] **TC-ADM-003** — Language switching works
  - Toggle language from TR to EN (or vice versa)
  - Verify: all UI labels update to selected language

- [ ] **TC-ADM-004** — Theme switching works
  - Toggle dark/light mode
  - Verify: UI theme changes consistently

- [ ] **TC-ADM-005** — Logout flow
  - Click logout button
  - Verify: redirected to Keycloak, session cleared

## 7.2 Identity Module UI

- [ ] **TC-ADM-006** — Users page — list users
  - Navigate to Users page
  - Verify: user table loads with pagination, platform admin visible

- [ ] **TC-ADM-007** — Users page — create user
  - Click "Create User", fill form, submit
  - Verify: user appears in list, success toast shown

- [ ] **TC-ADM-008** — Users page — view user detail
  - Click on a user row
  - Verify: detail page shows profile, organization memberships

- [ ] **TC-ADM-009** — Roles page — list and create role
  - Navigate to Roles page
  - Verify: roles listed with permission counts
  - Create new role with selected permissions

- [ ] **TC-ADM-010** — Organizations page — CRUD
  - Navigate to Organizations page
  - Create organization, view detail, update, add member, remove member

- [ ] **TC-ADM-011** — Tenants page — view tenants
  - Navigate to Tenants page
  - Verify: dev tenant visible with status

- [ ] **TC-ADM-012** — Audit logs page — filterable list
  - Navigate to Audit Logs page
  - Verify: logs visible, date range filter works

## 7.3 Contacts Module UI

- [ ] **TC-ADM-013** — Contact list — pagination and search
  - Navigate to Contacts page
  - Verify: contacts listed, search by name works, pagination works

- [ ] **TC-ADM-014** — Contact list — filter by status and type
  - Apply filters (status, type)
  - Verify: results filtered correctly, filters persist in URL

- [ ] **TC-ADM-015** — Contact detail — view and edit
  - Click on a contact
  - Verify: detail page loads with all fields
  - Edit contact, save, verify toast and updated data

- [ ] **TC-ADM-016** — Contact detail — addresses tab
  - Add address, edit address, delete address
  - Verify: all CRUD operations work

- [ ] **TC-ADM-017** — Contact detail — notes
  - Add note, pin note, edit note, delete note
  - Verify: notes list updates correctly

- [ ] **TC-ADM-018** — Tags page — CRUD
  - Navigate to Tags page
  - Create tag, update tag, assign to contact, remove from contact

- [ ] **TC-ADM-019** — Custom fields page — CRUD
  - Create custom field definition
  - Set custom field value on contact
  - Verify: field appears on contact detail

- [ ] **TC-ADM-020** — Import page — upload flow
  - Navigate to Import page
  - Select CSV file, get upload URL, upload to MinIO
  - Verify: import job starts, status trackable

- [ ] **TC-ADM-021** — Export page — trigger export
  - Navigate to Export page
  - Select format (CSV), start export
  - Verify: export job queued

## 7.4 Documents Module UI

- [ ] **TC-ADM-022** — Folder tree navigation
  - Navigate to Folders page
  - Verify: folder tree renders, can create/rename/delete folders

- [ ] **TC-ADM-023** — Document upload and browse
  - Navigate to Documents page
  - Upload a file via presigned URL flow
  - Verify: document appears in list

- [ ] **TC-ADM-024** — Document detail — version history and access
  - Open document detail
  - Verify: version history visible, can add version, manage access

## 7.5 Notifications Module UI

- [ ] **TC-ADM-025** — Notification list
  - Navigate to Notifications page
  - Verify: notifications listed with status badges

- [ ] **TC-ADM-026** — Template management
  - Navigate to Templates page
  - Create template, add translation, edit, delete

- [ ] **TC-ADM-027** — Provider configuration
  - Navigate to Providers page
  - Create/update provider, test connection

## 7.6 Reporting Module UI

- [ ] **TC-ADM-028** — Report list and create
  - Navigate to Reports page
  - Click "Create Report", fill form with SQL editor
  - Verify: SQL syntax highlighting works in CodeMirror editor
  - Verify: "Test Query" button executes SQL and shows preview table
  - Submit form, verify report appears in list

- [ ] **TC-ADM-029** — Report detail — execute, preview, download
  - Open report detail page
  - Click "Run Report", wait for completion
  - Verify: execution history shows Completed status
  - Click preview (Eye icon) → dialog opens with PDF/text content
  - Click download (Download icon) → file downloads

- [ ] **TC-ADM-030** — Report detail — edit and delete
  - Click edit (Pencil icon) → edit dialog opens with SQL editor
  - Modify query, test, save
  - Click delete (Trash icon) → confirmation dialog
  - Confirm delete → redirected to report list
