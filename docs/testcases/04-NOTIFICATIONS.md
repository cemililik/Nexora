# Suite 04 — Notification Engine

**Prerequisite:** Suite 03 passed (contacts exist for recipients)
**Execution Order:** 4
**Base URL:** `http://localhost:9080/api/v1/notifications`

---

## 4.1 Notification Providers

- [ ] **TC-NTF-001** — List providers (initially empty or seeded)
  - `GET /providers`
  - Expected: 200 with provider list

- [ ] **TC-NTF-002** — Create email provider
  - `POST /providers` with `{ name: "Dev Email", channel: "Email", providerName: "SendGrid", config: "{}", isDefault: true, dailyLimit: 1000 }`
  - Expected: 201 with provider object

- [ ] **TC-NTF-003** — Create SMS provider
  - `POST /providers` with `{ name: "Dev SMS", channel: "Sms", providerName: "Twilio", config: "{}", isDefault: true, dailyLimit: 500 }`
  - Expected: 201

- [ ] **TC-NTF-004** — Update provider
  - `PUT /providers/{id}` with updated dailyLimit
  - Expected: 200

- [ ] **TC-NTF-005** — Test provider connection
  - `POST /providers/{id}/test`
  - Expected: 200 (may fail if external service not configured — document expected)

## 4.2 Notification Templates

- [ ] **TC-NTF-006** — Create email template
  - `POST /templates` with `{ code: "welcome-email", name: "Welcome Email", channel: "Email", subject: "Welcome {{name}}", body: "Hello {{name}}, welcome to Nexora!", format: "HTML" }`
  - Expected: 201

- [ ] **TC-NTF-007** — Create template with duplicate code fails
  - `POST /templates` with same code
  - Expected: 400 or 409

- [ ] **TC-NTF-008** — List templates
  - `GET /templates?page=1&pageSize=10`
  - Expected: 200 with at least 1 template

- [ ] **TC-NTF-009** — Get template by ID
  - `GET /templates/{id}`
  - Expected: 200 with template details and translations

- [ ] **TC-NTF-010** — Update template
  - `PUT /templates/{id}` with updated body
  - Expected: 200

- [ ] **TC-NTF-011** — Add Turkish translation
  - `POST /templates/{id}/translations` with `{ languageCode: "tr", subject: "Hoş geldiniz {{name}}", body: "Merhaba {{name}}, Nexora'ya hoş geldiniz!" }`
  - Expected: 201

- [ ] **TC-NTF-012** — Delete template
  - `DELETE /templates/{id}` (create a temporary template first)
  - Expected: 200 or 204

## 4.3 Send Notifications

- [ ] **TC-NTF-013** — Send notification using template
  - `POST /notifications/send` with `{ templateCode: "welcome-email", recipientContactId: "{contact-id}", variables: { "name": "Ahmet" } }`
  - Expected: 200 or 201 with notification object, status = Queued

- [ ] **TC-NTF-014** — Send inline notification (no template)
  - `POST /notifications/send` with `{ channel: "Email", recipientContactId: "{contact-id}", subject: "Test", body: "Test message", inline: true }`
  - Expected: 200 or 201

- [ ] **TC-NTF-015** — List notifications
  - `GET /notifications?page=1&pageSize=10`
  - Expected: 200 with notifications from TC-NTF-013/014

- [ ] **TC-NTF-016** — Get notification detail
  - `GET /notifications/{id}`
  - Expected: 200 with full details including recipients and status

## 4.4 Bulk Notifications

- [ ] **TC-NTF-017** — Send bulk notification
  - `POST /bulk` with `{ templateCode: "welcome-email", contactIds: ["{id1}", "{id2}"], variables: { "name": "User" } }`
  - Expected: 200 or 201 with batch job ID

## 4.5 Scheduled Notifications

- [ ] **TC-NTF-018** — Schedule notification
  - `POST /schedule` with `{ templateCode: "welcome-email", recipientContactId: "{contact-id}", scheduledFor: "2026-04-01T09:00:00Z", variables: { "name": "Ahmet" } }`
  - Expected: 201 with scheduled notification

- [ ] **TC-NTF-019** — List scheduled notifications
  - `GET /schedule`
  - Expected: 200 with at least 1 scheduled notification

- [ ] **TC-NTF-020** — Cancel scheduled notification
  - `DELETE /schedule/{id}`
  - Expected: 200 or 204

## 4.6 Webhooks

- [ ] **TC-NTF-021** — SendGrid webhook (public endpoint)
  - `POST /webhooks/sendgrid` with SendGrid event payload (no auth required)
  - Expected: 200 (idempotent processing)

- [ ] **TC-NTF-022** — Twilio webhook (public endpoint)
  - `POST /webhooks/twilio` with Twilio status callback payload (no auth required)
  - Expected: 200
