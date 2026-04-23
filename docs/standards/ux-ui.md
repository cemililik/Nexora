# UX / UI Design Standard

Derives from: [ADR-007 Tab-Based Layout](../decisions/ADR-007-tab-based-layout-standard.md).
Ported from legacy `UX_UI_STANDARDS.md`; complements [code-style.md](code-style.md) §2
(tech-stack rules) and [localization.md](localization.md) §4 (UI string rules).

## 1. Design Principles

| Principle | Description |
|-----------|-------------|
| Consistency | All modules follow the same templates, components, and interactions. |
| Progressive disclosure | Summary first, detail on demand (tabs, expanders, modals). |
| Accessibility first | ARIA, keyboard nav, screen reader support on every interactive element. |
| Mobile-responsive | Desktop-first, responsive grid — usable on tablets and mobile. |
| RTL support | Tailwind `rtl:` utilities; layout mirrors correctly. |

## 2. Page Layout

### 2.1 Detail Pages — Tab Layout (Mandatory)

All entity detail pages MUST use the tab layout from
[ADR-007](../decisions/ADR-007-tab-based-layout-standard.md). Card-based side-by-side
layouts are not permitted for detail pages.

Rules:

- Use tabs when a detail page has 2+ content sections.
- **Max 5 tabs** per page. Consolidate small sections (tags, custom fields) into Overview.
- First tab is always **Overview** (or **Details**).
- Tab labels resolve via `t('lockey_module_tab_...')`.
- Tab content loads lazily — do not fetch data for inactive tabs.
- Tab state via `useState` — not URL params.
- Tab content MUST show `TabContentSkeleton` while loading.
- Tab switch MUST reset scroll position.

Implementation pattern (custom underline tabs — **not** shadcn/Radix `Tabs`):

```tsx
<div className="flex gap-1 border-b">
  {tabs.map((tab) => (
    <button
      key={tab.key}
      type="button"
      onClick={() => { setActive(tab.key); window.scrollTo({ top: 0, behavior: 'smooth' }); }}
      className={cn(
        'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
        active === tab.key
          ? 'border-primary text-primary'
          : 'border-transparent text-muted-foreground hover:text-foreground'
      )}
    >
      {tab.label}
    </button>
  ))}
</div>
```

Reference implementations: `ContactDetailPage`, `DocumentDetailPage`.

### 2.2 List Pages

- Search, filters, sort, pagination state in **URL query params** — not component state.
- Row click navigates to detail.
- Actions column is the last (rightmost) column.
- Create button top-right, `variant="default"`.
- `SearchInput` debounced 300 ms with clear button.
- Filter dropdowns use shadcn `Select` — never native `<select>`.
- Page-size options: `[10, 20, 50]`.
- Default sort deterministic (e.g. `createdAt desc`).
- **Every list page has a search field** even for client-side filter.
- Empty state uses `EmptyState`; a different message when filters are active and no match.

### 2.3 Create / Edit Pages

| Complexity | Pattern |
|-----------|---------|
| Simple (< 6 fields) | Modal (shadcn `Dialog`) |
| Complex (6+ fields or relations) | Dedicated page with form |

Form rules:

- React Hook Form + Zod.
- Labels above inputs; placeholder is never the only label.
- Required fields show red `*`.
- Hint text below label in `text-xs text-muted-foreground`.
- Error messages below the field in `text-destructive`.
- Use `FormField` wrapper for label + required + hint + error.
- Cancel / Submit at bottom-right. Submit shows spinner and is disabled while `isPending`.
- **Dirty-form navigation MUST be guarded with `useUnsavedChangesGuard`.**
- Character-limited textareas use `TextareaWithCounter`.

## 3. Status & Actions

### 3.1 Status Badges

Always use a dedicated `{Entity}StatusBadge`. Color mapping:

| Status | Color |
|--------|-------|
| Active / Published / Approved | Green |
| Inactive / Archived / Revoked | Gray |
| Error / Rejected / Failed | Red |
| Pending / Draft / Processing | Yellow |

Badges include **color + icon/text** — color alone never conveys meaning (accessibility).

### 3.2 Action Placement

| Context | Pattern |
|---------|---------|
| Page-level primary | Top-right header button or `DropdownMenu` |
| Destructive | `ConfirmDialog` required. For soft-delete entities use `useUndoableDelete` (5 s undo toast). |
| Inline table | Icon buttons in the Actions column |
| Edit-mode toggle | Edit icon swaps to Save/Cancel inside a bordered Card section |
| Logout | `ConfirmDialog` (destructive variant) |

### 3.3 Navigation

- **Breadcrumb required on every page.** Pattern: `Module > Resource List > Entity Name`.
- Back navigation is via breadcrumb — no separate Back button.
- Component: shadcn/ui `Breadcrumb`.

## 4. Empty States

Use `EmptyState` (`shared/components/feedback/EmptyState.tsx`) — never build inline.

Two variants per list page:

1. **No data at all** — entity icon + "No items yet" + CTA to create.
2. **Filters active, no match** — search icon + `lockey_common_no_results_filtered`.

## 5. Loading States

| Scenario | Component |
|----------|-----------|
| Initial page load | `LoadingSkeleton` (`Suspense` fallback) |
| Tab content | `TabContentSkeleton` (variants: `list`, `form`, `cards`) |
| Table data | `DataTable isLoading` (built-in skeleton) |
| Mutation in progress | Spinner inside disabled button |
| Route change | `Suspense` fallback in `AppLayout` |
| Modal dropdown search | `SearchableDropdown isLoading` |

Never show a blank page or tab — always a skeleton or spinner. Disable interactive
elements while mutations are pending.

## 6. Spacing & Typography

| Element | Value |
|---------|-------|
| Page padding | `p-6` |
| Section spacing | `space-y-6` |
| Card padding | `p-4` compact / `p-6` standard |
| Page title | `text-2xl font-semibold` |
| Section heading | `text-lg font-semibold` |
| Body | `text-sm` |
| Muted | `text-sm text-muted-foreground` |
| Inline gap | `gap-2` / `gap-4` |

## 7. Color & Theme

- Use shadcn/ui CSS variables only — no hardcoded hex/rgb.
- Dark mode via `class` strategy (`<html class="dark">`); all UI must look correct in both.

## 8. Accessibility

| Requirement | Implementation |
|-------------|----------------|
| ARIA labels | All interactive elements with `aria-label` or visible label text |
| Keyboard nav | Tab to focus, Enter/Space to activate, Escape to dismiss |
| Focus ring | `focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-background` |
| Color + icon | Status/errors use icon + color, not color alone |
| Screen readers | `sr-only` for visually hidden text |
| Semantic HTML | `<nav>`, `<main>`, `<section>`, `<article>` — not `<div>` for structure |
| Skip link | `AppLayout` MUST include `<a href="#main-content">` as the first focusable element |
| Table ARIA | `DataTable` accepts `aria-label`; sortable columns render `aria-sort` |
| Pagination ARIA | Previous/Next have `aria-label` (e.g. `lockey_common_previous_page`) |
| Combobox | `SearchableDropdown` implements `role="combobox"` + `role="listbox"` + `role="option"` |

## 9. Shared Component Inventory

All reusable UI lives in `shared/components/`. Use these instead of building inline.

### 9.1 Data (`shared/components/data/`)

| Component | Purpose |
|-----------|---------|
| `DataTable<T>` | Generic table with pagination, sort, bulk select, loading, empty state |
| `SearchInput` | Debounced search with clear |
| `SearchableDropdown<T>` | Searchable dropdown with loading/empty/keyboard nav |
| `FormField` | Label + required `*` + hint + error wrapper |
| `TextareaWithCounter` | Textarea with live character counter |

### 9.2 Feedback (`shared/components/feedback/`)

| Component | Purpose |
|-----------|---------|
| `EmptyState` | Centered empty state with icon, title, description, CTA |
| `TabContentSkeleton` | Skeleton for tab content (`list` / `form` / `cards`) |
| `LoadingSkeleton` | Generic line-based skeleton |
| `ConfirmDialog` | Confirmation for destructive / significant actions |
| `ErrorBoundary` | Class component error boundary with telemetry |

### 9.3 Hooks (`shared/hooks/`)

| Hook | Purpose |
|------|---------|
| `useUnsavedChangesGuard(isDirty)` | Blocks navigation on dirty forms |
| `useUndoableDelete({...})` | Wraps soft-delete with 5 s undo toast |
| `usePagination(defaultPageSize)` | URL-based pagination state |
| `useApiError()` | Uniform API error handling (toast + form field errors) |
| `usePermissions()` | Permission checks (UX only) |

### 9.4 Layout (`shared/components/layout/`)

| Component | Notes |
|-----------|-------|
| `AppLayout` | Skip-to-content link, `<main id="main-content">`, ErrorBoundary + Suspense |
| `Topbar` | User menu shows email under name, logout via ConfirmDialog |
| `Sidebar` | Collapse state persisted to localStorage via Zustand |
| `Breadcrumbs` | RTL-safe (ChevronRight rotates 180°) |

## 10. Migration From Card Layouts

Legacy detail pages using card-based side-by-side layouts migrate to the tab pattern
(§2.1). Completed: Identity (User, Role, Organization, Tenant), Notifications Template,
Reporting Report.

Migration steps:

1. Identify content sections.
2. Group into ≤ 5 tabs.
3. Replace card grid with the custom underline tab pattern.
4. `useState` for tab state (keep URL clean).
5. First tab holds primary info.
6. Header: name + badges on left, actions on right.
7. Add new tab-label translation keys in `en` + `tr`.
