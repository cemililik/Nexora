# Suite 03 — Contact Management

**Prerequisite:** Suite 02 passed (user and org exist)
**Execution Order:** 3
**Base URL:** `http://localhost:9080/api/v1/contacts`

---

## 3.1 Contact CRUD

- [ ] **TC-CON-001** — Create individual contact
  - `POST /contacts` with `{ type: "Individual", firstName: "Ahmet", lastName: "Yılmaz", email: "ahmet@test.com", language: "tr", currency: "TRY" }`
  - Expected: 201 with contact object, status = Active

- [ ] **TC-CON-002** — Create organization contact
  - `POST /contacts` with `{ type: "Organization", companyName: "Nexora Ltd.", email: "info@nexora.com", language: "en", currency: "USD" }`
  - Expected: 201 with contact object

- [ ] **TC-CON-003** — Create individual without firstName fails
  - `POST /contacts` with `{ type: "Individual", lastName: "Test" }` (missing firstName)
  - Expected: 400 validation error

- [ ] **TC-CON-004** — Create organization without companyName fails
  - `POST /contacts` with `{ type: "Organization", firstName: "Test" }` (missing companyName)
  - Expected: 400 validation error

- [ ] **TC-CON-005** — List contacts with pagination
  - `GET /contacts?page=1&pageSize=10`
  - Expected: 200 with paginated result, items include contacts from TC-CON-001/002

- [ ] **TC-CON-006** — Search contacts by name
  - `GET /contacts?search=Ahmet`
  - Expected: 200 with filtered results containing TC-CON-001

- [ ] **TC-CON-007** — Get contact by ID
  - `GET /contacts/{id}` using ID from TC-CON-001
  - Expected: 200 with full contact details

- [ ] **TC-CON-008** — Get contact 360 view
  - `GET /contacts/{id}/360`
  - Expected: 200 with aggregated view (activities, notes, history)

- [ ] **TC-CON-009** — Update contact
  - `PUT /contacts/{id}` with `{ firstName: "Mehmet", lastName: "Yılmaz", phone: "+905551234567" }`
  - Expected: 200 with updated data

- [ ] **TC-CON-010** — Archive contact
  - `DELETE /contacts/{id}` (using TC-CON-002 org contact)
  - Expected: 200, contact status = Archived

- [ ] **TC-CON-011** — Archive already archived contact fails
  - `DELETE /contacts/{id}` again on same contact
  - Expected: 400 or 422 — already archived

- [ ] **TC-CON-012** — Restore archived contact
  - `POST /contacts/{id}/restore`
  - Expected: 200, contact status = Active

## 3.2 Contact Addresses

- [ ] **TC-CON-013** — Add address to contact
  - `POST /contacts/{contactId}/addresses` with `{ type: "Home", street1: "Atatürk Cad. No:1", city: "Istanbul", countryCode: "TR", postalCode: "34000" }`
  - Expected: 201 with address object

- [ ] **TC-CON-014** — Add second address (work)
  - `POST /contacts/{contactId}/addresses` with `{ type: "Work", street1: "Levent Plaza", city: "Istanbul", countryCode: "TR", isPrimary: true }`
  - Expected: 201

- [ ] **TC-CON-015** — List contact addresses
  - `GET /contacts/{contactId}/addresses`
  - Expected: 200 with 2 addresses

- [ ] **TC-CON-016** — Update address
  - `PUT /contacts/{contactId}/addresses/{addressId}` with updated city
  - Expected: 200

- [ ] **TC-CON-017** — Delete address
  - `DELETE /contacts/{contactId}/addresses/{addressId}`
  - Expected: 200 or 204

## 3.3 Tags

- [ ] **TC-CON-018** — Create tag
  - `POST /tags` with `{ name: "VIP", category: "Priority", color: "#FFD700" }`
  - Expected: 201

- [ ] **TC-CON-019** — Create second tag
  - `POST /tags` with `{ name: "Newsletter", category: "Marketing" }`
  - Expected: 201

- [ ] **TC-CON-020** — List tags
  - `GET /tags`
  - Expected: 200 with at least 2 tags

- [ ] **TC-CON-021** — Assign tag to contact
  - `POST /contacts/{contactId}/tags/{tagId}`
  - Expected: 200

- [ ] **TC-CON-022** — Assign same tag again fails
  - `POST /contacts/{contactId}/tags/{tagId}` (duplicate)
  - Expected: 400 or 409 — already assigned

- [ ] **TC-CON-023** — Remove tag from contact
  - `DELETE /contacts/{contactId}/tags/{tagId}`
  - Expected: 200

## 3.4 Relationships

- [ ] **TC-CON-024** — Add relationship between contacts
  - `POST /contacts/{contactId}/relationships` with `{ relatedContactId: "{org-contact-id}", type: "Employer" }`
  - Expected: 201

- [ ] **TC-CON-025** — List relationships
  - `GET /contacts/{contactId}/relationships`
  - Expected: 200 with at least 1 relationship

- [ ] **TC-CON-026** — Self-relationship fails
  - `POST /contacts/{contactId}/relationships` with `{ relatedContactId: "{same-contact-id}" }`
  - Expected: 400

## 3.5 Communication Preferences & Consent

- [ ] **TC-CON-027** — Record consent
  - `POST /contacts/{contactId}/consents` with `{ consentType: "Email", granted: true, source: "Web Form" }`
  - Expected: 201

- [ ] **TC-CON-028** — List consent records
  - `GET /contacts/{contactId}/consents`
  - Expected: 200 with consent history

## 3.6 Notes

- [ ] **TC-CON-029** — Add note to contact
  - `POST /contacts/{contactId}/notes` with `{ content: "Met at conference, very interested." }`
  - Expected: 201

- [ ] **TC-CON-030** — Pin note
  - `PUT /contacts/{contactId}/notes/{noteId}` with `{ isPinned: true }`
  - Expected: 200

## 3.7 Import/Export

- [ ] **TC-CON-031** — Get import upload URL
  - `POST /contacts/import/upload-url` with `{ fileName: "contacts.csv", contentType: "text/csv" }`
  - Expected: 200 with presigned URL

- [ ] **TC-CON-032** — Start contact export
  - `POST /contacts/export` with `{ format: "Csv" }`
  - Expected: 200 or 201 with job ID

## 3.8 Duplicate Detection & Merge

- [ ] **TC-CON-033** — Find duplicates
  - `GET /contacts/{contactId}/duplicates?threshold=40`
  - Expected: 200 (may be empty if no duplicates)

- [ ] **TC-CON-034** — Merge contacts
  - Create a duplicate contact with same email, then:
  - `POST /contacts/merge` with `{ primaryContactId: "{id1}", secondaryContactId: "{id2}" }`
  - Expected: 200, secondary contact absorbed

## 3.9 GDPR

- [ ] **TC-CON-035** — Request GDPR data export
  - `POST /contacts/{contactId}/gdpr/export`
  - Expected: 200 or 202 accepted
