# Suite 08 — Portal Framework (nexora-portal) UI Tests

**Prerequisite:** Suite 02 passed (Keycloak auth working)
**Execution Order:** 8
**URL:** `http://localhost:3000`

---

## 8.1 Authentication

- [ ] **TC-PRT-001** — Login flow
  - Navigate to `http://localhost:3000`
  - Verify: redirected to login page
  - Click "Sign in" → Keycloak login
  - Enter `platformadmin@nexora.dev` / `Admin123!`
  - Verify: redirected to dashboard, no login loop

- [ ] **TC-PRT-002** — Session persists on page refresh
  - After login, refresh the page (F5)
  - Verify: stays on dashboard, no redirect to login

- [ ] **TC-PRT-003** — API calls succeed after login
  - Open browser DevTools → Console
  - Verify: no 401 errors on API calls
  - Verify: tenant modules and organization data loaded

- [ ] **TC-PRT-004** — Logout flow
  - Click user menu → Log out
  - Verify: redirected to login page
  - Verify: accessing `/dashboard` redirects to login

## 8.2 Dashboard

- [ ] **TC-PRT-005** — Dashboard renders
  - After login, verify dashboard page loads
  - Verify: "Welcome, {firstName}" message displayed
  - Verify: no JavaScript errors in console

- [ ] **TC-PRT-006** — Dashboard layout
  - Verify: sidebar, topbar, and main content area visible
  - Verify: sidebar navigation items displayed based on installed modules

## 8.3 Profile

- [ ] **TC-PRT-007** — Profile page renders
  - Navigate to Profile (sidebar or user menu)
  - Verify: user name, email displayed correctly
  - Verify: organization name and currency displayed (if org loaded)

## 8.4 Multi-Language

- [ ] **TC-PRT-008** — Language switching (EN → TR)
  - Switch language to Turkish
  - Verify: all labels change to Turkish translations
  - Verify: URL changes to `/tr/dashboard`

- [ ] **TC-PRT-009** — Language persists on navigation
  - After switching to TR, navigate to Profile
  - Verify: URL stays with `/tr/` prefix, labels remain Turkish

## 8.5 Branding

- [ ] **TC-PRT-010** — Tenant logo displayed
  - Verify: tenant logo or fallback initials visible in sidebar/topbar

- [ ] **TC-PRT-011** — Responsive layout
  - Resize browser to mobile width
  - Verify: sidebar collapses, hamburger menu appears
  - Verify: content remains usable

## 8.6 Error Handling

- [ ] **TC-PRT-012** — 404 page
  - Navigate to `/en/nonexistent-page`
  - Verify: 404 error page displayed (not a blank page)

- [ ] **TC-PRT-013** — Error boundary catches crashes
  - If a component error occurs, verify ErrorBoundary shows fallback UI
  - Verify: "Try again" button visible

- [ ] **TC-PRT-014** — Incognito/private browsing
  - Open portal in incognito window
  - Complete full login → dashboard flow
  - Verify: works identically to normal browsing (no cookie conflicts)
