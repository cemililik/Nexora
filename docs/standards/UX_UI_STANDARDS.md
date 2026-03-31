# Nexora - UX/UI Design Standards

> Complements [FRONTEND_STANDARDS.md](FRONTEND_STANDARDS.md) (tech stack, state management, code patterns).
> This document covers **layout, interaction, and visual design** decisions.

## 1. Design Principles

| Principle | Description |
|-----------|-------------|
| **Consistency** | All modules follow the same layout templates, component patterns, and interaction behaviors. |
| **Progressive Disclosure** | Show summary first, reveal detail on demand (tabs, expandable sections, modals). |
| **Accessibility First** | ARIA attributes, keyboard navigation, screen reader support on every interactive element. |
| **Mobile-Responsive** | Desktop-first design with responsive grid — usable on tablets and mobile. |
| **RTL Support** | Use Tailwind `rtl:` utilities; layout must mirror correctly for Arabic and other RTL locales. |

## 2. Page Layout Standards

### 2.1 Detail Pages — Tab-Based Layout (Mandatory)

All entity detail pages MUST use the tab-based layout. Card-based side-by-side layouts are **not permitted** for detail pages.

**Standard Template:**

```
┌─────────────────────────────────────────────┐
│ Breadcrumb > Module > Resource > Name       │
├─────────────────────────────────────────────┤
│ [Icon] Entity Name              [Actions ▼] │
│ Status Badge · Metadata · Metadata          │
├─────────────────────────────────────────────┤
│ [Tab 1] [Tab 2] [Tab 3] [Tab 4]           │
├─────────────────────────────────────────────┤
│                                             │
│  Tab Content Area                           │
│  (Cards, Tables, Forms as needed)           │
│                                             │
└─────────────────────────────────────────────┘
```

**Rules:**

- Use tabs when a detail page has 2 or more content sections — never cards side by side.
- Maximum **6 tabs** per page. If you need more, consolidate related sections.
- First tab is always **"Overview"** or **"Details"** (the primary information).
- Tab labels MUST use translation keys (`t('lockey_module_tab_overview')`).
- Each tab loads content lazily — do not fetch data for inactive tabs.
- Tab state uses `useState` (not URL params — keep URL clean).

**Implementation pattern** (custom underline tabs — consistent with ContactDetailPage/DocumentDetailPage):

```tsx
import { cn } from '@/shared/lib/utils';

type TabKey = 'overview' | 'history';

export default function EntityDetailPage() {
  const { t } = useTranslation('module');
  const [activeTab, setActiveTab] = useState<TabKey>('overview');

  const tabs: { key: TabKey; label: string }[] = [
    { key: 'overview', label: t('lockey_module_tab_overview') },
    { key: 'history', label: t('lockey_module_tab_history') },
  ];

  return (
    <div className="space-y-6">
      {/* Header — entity name, badges, and actions grouped logically */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{entity.name}</h1>
          <div className="flex items-center gap-2 mt-1">
            <StatusBadge status={entity.status} />
            <span className="text-sm text-muted-foreground">{metadata}</span>
          </div>
        </div>
        <div className="flex gap-2">
          {/* Action buttons */}
        </div>
      </div>

      {/* Tab navigation — underline style */}
      <div className="flex gap-1 border-b">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => setActiveTab(tab.key)}
            className={cn(
              'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
              activeTab === tab.key
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab content — conditional rendering */}
      {activeTab === 'overview' && <OverviewContent entity={entity} />}
      {activeTab === 'history' && <HistoryContent entityId={entity.id} />}
    </div>
  );
}
```

**Do NOT use** shadcn/Radix UI `Tabs` component — use the custom underline pattern above for visual consistency across all modules.

### 2.2 List Pages

**Standard Template:**

```
┌─────────────────────────────────────────────┐
│ Breadcrumb > Module > Resources             │
├─────────────────────────────────────────────┤
│ [Search...] [Filter ▼] [Filter ▼] [+Create]│
├─────────────────────────────────────────────┤
│ DataTable with sortable columns             │
│ Clickable rows → navigate to detail         │
├─────────────────────────────────────────────┤
│ [◄ Prev] Page 1 of 5 [Next ►] [10▼/page]  │
└─────────────────────────────────────────────┘
```

**Rules:**

- Search, filter, sort, and pagination state MUST be stored in URL query params — not component state.
- Row click navigates to the detail page.
- **Actions column** is always the last (rightmost) column.
- **Create button** is top-right, `variant="default"` (primary).
- Search input is debounced at **300ms**.
- Page size options: `[10, 20, 50]`.
- Default sort must be deterministic (e.g., `createdAt desc`).

### 2.3 Create/Edit Pages

| Entity Complexity | Pattern | Example |
|-------------------|---------|---------|
| Simple (< 6 fields) | Modal dialog (shadcn `Dialog`) | Create Role, Add Tag |
| Complex (6+ fields, relations) | Dedicated page with form | Create Contact, Create Document |

**Form Rules:**

- All forms use **React Hook Form + Zod** (see FRONTEND_STANDARDS.md §6.4).
- Labels are **above inputs** — never use placeholder text as the only label.
- Error messages display **below the field** in `text-destructive` color.
- Cancel and Submit buttons at the **bottom-right** of the form.
- Submit button shows a spinner and is disabled during `isPending`.
- Cancel navigates back (or closes modal) without prompting unless the form is dirty.

## 3. Component Standards

### 3.1 Status Display

- Always use a dedicated `{Entity}StatusBadge` component per entity type.
- Consistent color mapping across the entire application:

| Status | Color | Tailwind Class |
|--------|-------|----------------|
| Active / Published / Approved | Green | `bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300` |
| Inactive / Archived / Revoked | Gray | `bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-300` |
| Error / Rejected / Failed | Red | `bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300` |
| Pending / Draft / Processing | Yellow | `bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300` |

- Badges MUST include both color and an icon/text — color alone must not convey meaning (accessibility).

### 3.2 Actions

| Context | Pattern |
|---------|---------|
| Page-level primary actions | Top-right of page header (Button or DropdownMenu) |
| Destructive actions (delete, revoke) | Require `ConfirmDialog` with explicit confirmation |
| Inline table actions | Icon buttons in the Actions column (last column) |
| Edit mode toggle | Edit icon button switches content to Save/Cancel mode |

### 3.3 Navigation

- **Breadcrumb** is required on every page.
- Pattern: `Module > Resource List > Entity Name`
- Back navigation is done via breadcrumb links — do not add a separate "Back" button.
- Breadcrumb component: shadcn/ui `Breadcrumb`.

### 3.4 Empty States

- Centered layout with an icon, descriptive text, and a call-to-action.
- All strings use translation keys.

```tsx
<div className="flex flex-col items-center justify-center py-12 text-center">
  <UsersIcon className="h-12 w-12 text-muted-foreground" />
  <h3 className="mt-4 text-lg font-semibold">
    {t('lockey_contacts_empty_title')}
  </h3>
  <p className="mt-2 text-sm text-muted-foreground">
    {t('lockey_contacts_empty_description')}
  </p>
  <Button className="mt-6" onClick={onCreate}>
    {t('lockey_contacts_empty_action')}
  </Button>
</div>
```

### 3.5 Loading States

| Scenario | Pattern |
|----------|---------|
| Initial page/tab load | Skeleton loader (shadcn `Skeleton`) matching content shape |
| Mutation in progress | Spinner inside the submit button + button disabled |
| Navigation / route change | Top progress bar or `Suspense` fallback |

- Never show a blank page — always show a skeleton or spinner.
- Disable interactive elements while mutations are pending.

## 4. Spacing & Typography

| Element | Value |
|---------|-------|
| Page padding | `p-6` |
| Section spacing | `space-y-6` |
| Card padding | `p-4` (compact) or `p-6` (standard) |
| Page title | `text-2xl font-semibold` |
| Section heading | `text-lg font-semibold` |
| Body text | `text-sm` |
| Muted/secondary text | `text-sm text-muted-foreground` |
| Gap between inline items | `gap-2` (tight) or `gap-4` (standard) |

## 5. Color & Theme

- Use **shadcn/ui CSS variables** exclusively — never hardcode hex/rgb values.
- Status colors follow the mapping in section 3.1.
- Dark mode is supported via the `class` strategy (`<html class="dark">`).
- All custom UI must look correct in both light and dark themes.
- Refer to `globals.css` for the full token list (see FRONTEND_STANDARDS.md §7.2).

## 6. Accessibility

| Requirement | Implementation |
|-------------|----------------|
| ARIA labels | All interactive elements (`button`, `input`, `link`) must have `aria-label` or visible label text |
| Keyboard navigation | `Tab` to move focus, `Enter`/`Space` to activate, `Escape` to close modals/dropdowns |
| Focus indicators | Visible focus ring (`focus-visible:ring-2 focus-visible:ring-ring`) on all focusable elements |
| Color + icon | Status and errors must use icon/text alongside color — never color alone |
| Screen readers | Use `sr-only` class for visually hidden but screen-reader-accessible text |
| Semantic HTML | Use `<nav>`, `<main>`, `<section>`, `<article>` — not generic `<div>` for structural elements |

## 7. Internationalization (i18n)

- **Zero hardcoded strings** — every user-visible string uses `t('lockey_...')`.
- Key format: `lockey_{module}_{context}_{descriptor}` (see LOCALIZATION_STANDARDS.md).
- RTL layout: use Tailwind `rtl:` utilities for directional spacing and alignment.
- Tab labels, breadcrumbs, button text, empty states, error messages — all translated.
- New UI components must have translation keys added to both `en` and `tr` files before merge.

## 8. Migration Guide: Card-Based to Tab-Based Detail Pages

Existing detail pages that use a card-based layout (e.g., `UserDetailPage`, `RoleDetailPage` in the Identity module) must be migrated to the tab-based template.

**Migration steps:**

1. Identify all content sections currently displayed as side-by-side cards.
2. Group related sections into tabs (max 6).
3. Replace the card grid with the **custom underline tab pattern** (see Section 2.1). Do NOT use shadcn/Radix `Tabs` — use `<button>` elements with `border-b-2` styling for visual consistency.
4. Use `useState` for tab state (keep URL clean).
5. Ensure the first tab ("Overview" or "Details") contains the most important information.
6. Header layout: entity name + badges grouped on the left, action buttons on the right. Keep related information visually close — don't spread heading far-left and metadata far-right.
7. Update translation files with new tab label keys.

**Completed:** Identity module pages (UserDetailPage, RoleDetailPage, OrganizationDetailPage, TenantDetailPage), Notifications TemplateDetailPage, Reporting ReportDetailPage — all migrated.
