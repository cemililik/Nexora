# ADR-007: UX/UI Tab-Based Layout Standard

## Status
Accepted

## Date
2026-03-31

## Context

Detail page layouts were inconsistent across modules. The Identity module used a card-based side-by-side layout, the Documents module used tabs, and the Contacts module also used tabs but with a different implementation. This inconsistency created a fragmented user experience and made it harder for developers to build new detail pages without debating layout choices.

## Decision

We will standardize **all detail pages** on a **custom underline tab pattern**.

The tab implementation uses native `<button>` elements with `border-b-2` for the active indicator and `useState` for tab state management. Tab state is component-local (not URL params).

### Implementation Pattern

```tsx
const [activeTab, setActiveTab] = useState('overview');

const tabs = [
  { key: 'overview', label: t('lockey_module_tab_overview') },
  { key: 'details',  label: t('lockey_module_tab_details') },
  { key: 'history',  label: t('lockey_module_tab_history') },
];
```

Tabs are rendered as buttons with conditional `border-b-2 border-primary` styling on the active tab. This is explicitly **not** Radix UI Tabs or shadcn/ui Tabs — we use a simpler custom pattern to avoid the additional dependency and maintain full control over styling.

Reference implementations: `ContactDetailPage`, `DocumentDetailPage`.

## Consequences

### Positive
- **Visual consistency**: Every detail page looks and behaves the same across all modules
- **Simpler code**: Plain `useState` + conditional classes — no wrapper component library needed
- **No additional dependency**: Avoids Radix UI Tabs component and its accessibility abstractions
- **Developer clarity**: One pattern to follow, no layout debates per module

### Negative
- **Manual accessibility**: Developers must add ARIA attributes (`role="tablist"`, `aria-selected`) manually since we are not using Radix UI's built-in accessibility
- **No URL persistence**: Tab state resets on page refresh (by design — keeps URLs clean)

### Risks
- **Inconsistent adoption**: A developer may use Radix Tabs or a card layout by habit. Mitigation: code review enforcement, reference to this ADR and `UX_UI_STANDARDS.md`.

## Alternatives Considered

| Alternative | Pros | Cons | Why Rejected |
|------------|------|------|-------------|
| Radix UI / shadcn Tabs | Built-in accessibility, composable | Additional dependency, opinionated styling harder to override | Adds complexity for a simple UI pattern |
| Card-based side-by-side layout | All info visible at once | Doesn't scale for entities with many sections, inconsistent with existing modules | Poor scalability, already abandoned by most modules |
| URL-param-based tabs | Shareable deep links to specific tabs | URL clutter, unnecessary for internal admin detail pages | Over-engineering for the use case |

## Related
- [UX/UI Design Standards](../standards/UX_UI_STANDARDS.md)
- Reference: `src/Clients/nexora-admin/src/modules/contacts/pages/ContactDetailPage.tsx`
- Reference: `src/Clients/nexora-admin/src/modules/documents/pages/DocumentDetailPage.tsx`
