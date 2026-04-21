---
name: create-admin-page
description: Scaffold a frontend page (List, Detail, or Create/Edit) in nexora-admin or nexora-portal following UX_UI_STANDARDS.md, FRONTEND_STANDARDS.md, and API_INTEGRATION_STANDARDS.md. Use when the user asks to "add a page", "create list/detail page", or "build a screen for X".
---

# Create Admin Page

Creates a page in `src/Clients/nexora-admin/src/modules/{module}/pages/` (or `nexora-portal`) with the correct template, hooks, and components.

## Decide template first
- **List page** — DataTable + SearchInput + shadcn Select filters; URL-params for filter/search/pagination; clickable rows.
- **Detail page** — **Custom underline tab layout** (`<button class="border-b-2">`), NOT shadcn/Radix Tabs, NOT cards side-by-side. Max 5 tabs. Tab state via `useState`. `TabContentSkeleton` while loading. Reset scroll on switch. Reference: `ContactDetailPage`, `DocumentDetailPage`.
- **Create/Edit form** — React Hook Form + Zod; `FormField` wrapper; `useUnsavedChangesGuard(isDirty)` mandatory.

## Required imports/components
- Layout: `AppLayout`, `Breadcrumbs` (every page).
- Data: `DataTable`, `SearchInput`, `SearchableDropdown`, `FormField`, `TextareaWithCounter`.
- Feedback: `EmptyState` (never inline empty text), `TabContentSkeleton`, `LoadingSkeleton`, `ConfirmDialog`.
- Hooks: `useAuth`, `usePermissions`, `usePagination`, `useUnsavedChangesGuard`, `useUndoableDelete`.

## API integration rules
- Custom hooks: `useXs()`, `useX(id)`, `useCreateX()`, `useUpdateX()`, `useDeleteX()`.
- Use the shared query key factory; invalidate after mutations.
- All responses unwrap `ApiEnvelope<T>.data`.
- Auth gate: check `!token || token.error === 'RefreshAccessTokenError'` (see §4 of API_INTEGRATION_STANDARDS).
- Use `useApiError` — no manual error parsing.
- Validation errors → `setError()` on form fields.
- File upload → presigned URL flow (no direct multipart).

## Mandatory UX rules
- TypeScript strict, no `any`, functional components only.
- **Zero raw text in JSX** — use `t('lockey_...')` for every label/button/toast.
- Tailwind + shadcn/ui only; `cn()` for conditional classes; no inline `style={}`.
- Responsive grids: `grid-cols-1 sm:grid-cols-2` (never bare `grid-cols-2`).
- Status badges: green=active, gray=inactive, red=error, yellow=pending.
- ARIA labels, keyboard nav, focus indicators; color+icon together (not color alone).

## File conventions
- `PascalCase.tsx` components, `useX.ts` hooks, co-located `X.test.tsx`.
- Shared = named export; page = default export.
- Props interface: `{ComponentName}Props` in same file.

## Before finishing
- Add lockey keys to `locales/en/{module}.json` and `locales/tr/{module}.json`.
- Register route + nav in the module's `manifest.ts`.
- Run `npm run lint` — must pass (see pre-commit-verify skill).
