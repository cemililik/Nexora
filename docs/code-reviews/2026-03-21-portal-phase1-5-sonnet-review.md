# AI Agent Fix Instructions: Portal Framework Phase 1.5 — Second-Pass Code Review

**Date**: 2026-03-21
**Reviewer**: Claude Sonnet 4.6 (second-pass review)
**Commits Reviewed**:
- `b0a82893` — `feat(portal): implement Phase 1.5 Portal Framework with Next.js 16`
- `c7bbab0e` — `fix(portal): resolve all code review findings from Phase 1.5 review`
**Branch**: `development`
**Prior Review**: `docs/code-reviews/2026-03-21-portal-framework-phase1-5-review.md`
**Standards Referenced**: `FRONTEND_STANDARDS.md`, `LOCALIZATION_STANDARDS.md`, `CODING_STANDARDS.md`, `API_INTEGRATION_GUIDE.md`, `MODULE_SYSTEM.md`, `ARCHITECTURE/OVERVIEW.md`, `CLAUDE.md`

---

## Context and Background

You are an AI agent tasked with fixing all remaining issues in the Nexora Portal Framework after two commits. The first commit (`b0a82893`) implemented the Phase 1.5 portal infrastructure. A prior code review identified critical, major, and minor issues. The second commit (`c7bbab0e`) attempted to fix those issues and added new tests.

Your job, after reading this document, is to fix ALL remaining issues listed below. You must NOT look at the code again — all relevant file paths, line numbers, current code, and required fixes are included in this document.

### What the Second Commit Fixed (Verified Resolved)

The following issues from the prior review were correctly resolved in `c7bbab0e`:

| Prior Issue | Status | Evidence |
|------------|--------|----------|
| CRITICAL-1: `middleware.ts` — no server-side auth | RESOLVED | `auth(req => ...)` wrapping with redirect logic |
| CRITICAL-2: `ErrorBoundary.tsx` — hardcoded strings | RESOLVED | `ErrorBoundaryFallback` functional component using `useTranslations()` |
| CRITICAL-3: `page.tsx` — hardcoded `locale: 'en'` | RESOLVED | Now reads `locale` from `params` |
| CRITICAL-4: `next.config.ts` — wildcard `hostname: '**'` | RESOLVED | Changed to `process.env.IMAGE_HOSTNAME ?? 'cdn.nexora.io'` |
| CRITICAL-5: `auth.ts` — manual JWT decode | RESOLVED | Removed; now uses `profile` from Keycloak ID token |
| CRITICAL-5b: `auth.ts` — refresh error not propagated | RESOLVED | Now returns `{ ...token, error: 'RefreshAccessTokenError' }` |
| CRITICAL-6: `Topbar.tsx` — language switcher no `value` | RESOLVED | `value={currentLocale}` and `useLocale()` added |
| MAJOR: `(portal)/layout.tsx` — client component | RESOLVED | Converted to server component using `await auth()` |
| MAJOR: `BrandingProvider.tsx` — CSS injection | RESOLVED | `isSafeUrl()` validation added |
| MAJOR: `Sidebar.tsx` — filter logic bug | RESOLVED | Uses `canAccessModule ? m.navigation : []` pattern |
| MAJOR: `useAuth.ts` — infinite loop risk | RESOLVED | `fetchFailed` state prevents retry loop |
| MAJOR: `useOrganization.ts` — missing `staleTime` | RESOLVED | `staleTime: 5 * 60 * 1000` added |
| MAJOR: `useModules.ts` — `hasModule` not memoized | RESOLVED | Wrapped in `useCallback` |
| MAJOR: Missing critical tests | RESOLVED | 4 new test files added (RequireAuth, RequirePermission, SectionRenderer, useModules logic) |
| MAJOR: `@vitest/coverage-v8` missing | RESOLVED | Added to `devDependencies` |
| MINOR: `TenantLogo.tsx` — hardcoded 'N' placeholder | RESOLVED | Skeleton div while loading |

### New Issues Introduced by the Second Commit

The second commit introduced new bugs and left some prior issues unaddressed. All remaining issues are catalogued below.

---

## Severity Legend

- **CRITICAL**: Security vulnerability, data integrity risk, deployment blocker, or zero-tolerance standards violation — fix before any merge
- **MAJOR**: Architectural problem, business logic error, or clear standards deviation — fix before merge
- **MINOR**: Code quality issue or minor standards deviation — fix but not a merge blocker
- **SUGGESTION**: Best practice improvement — optional but recommended

---

## Findings Summary

| Category | Critical | Major | Minor | Suggestion | Total |
|----------|----------|-------|-------|-----------|-------|
| Security | 1 | 1 | 0 | 0 | 2 |
| Architecture / Next.js | 0 | 3 | 2 | 1 | 6 |
| Business Logic | 0 | 2 | 1 | 0 | 3 |
| Standards Compliance | 1 | 1 | 2 | 0 | 4 |
| Type Safety | 0 | 1 | 2 | 0 | 3 |
| Testing | 0 | 3 | 2 | 1 | 6 |
| i18n / Localization | 1 | 1 | 1 | 0 | 3 |
| Performance | 0 | 1 | 1 | 1 | 3 |
| Error Handling | 0 | 2 | 1 | 0 | 3 |
| Code Quality | 0 | 0 | 3 | 2 | 5 |
| **Total** | **3** | **15** | **15** | **5** | **38** |

---

## CATEGORY 1: SECURITY

### [CRITICAL] SEC-1 — `middleware.ts`: Locale Extraction From URL is Fragile and Bypassable

**File**: `src/Clients/nexora-portal/src/middleware.ts`
**Lines**: 26–28

**Current code**:
```ts
if (!isAuth) {
  const locale = pathname.split('/')[1] || 'en';
  const loginUrl = new URL(`/${locale}/auth/login`, req.url);
  return NextResponse.redirect(loginUrl);
}
```

**Problem**: The locale is extracted by splitting the pathname and taking the first segment (`pathname.split('/')[1]`). This is insecure and fragile for three reasons:

1. **Bypass vector**: A request to `/../../etc/passwd` or `/javascript:alert(1)/auth/login` would pass `pathname.split('/')[1]` as `'javascript:alert(1)'` into the URL, creating a crafted redirect URL. While `new URL(...)` provides some protection, the constructed path `/javascript:alert(1)/auth/login` is still invalid and could cause unexpected behavior.

2. **Non-locale injection**: A crafted path like `/admin/sensitive-data` would result in a redirect to `/admin/auth/login` — a non-existent path — instead of the correct locale-prefixed login URL. The user gets a 404 instead of a proper redirect.

3. **The locale is already available**: When `next-intl` middleware processes the request, the locale is available in `req.nextUrl`. The correct approach is to validate the extracted segment against the known locale list.

**Required fix** — validate the extracted locale against the routing config:
```ts
import { routing } from './i18n/routing';

// Inside auth middleware callback:
if (!isAuth) {
  const segments = pathname.split('/');
  const potentialLocale = segments[1];
  const locale = routing.locales.includes(potentialLocale as 'en' | 'tr')
    ? potentialLocale
    : routing.defaultLocale;
  const loginUrl = new URL(`/${locale}/auth/login`, req.url);
  return NextResponse.redirect(loginUrl);
}
```

---

### [MAJOR] SEC-2 — `api.ts`: 401 Redirect Uses Absolute `/auth/login` Without Locale Prefix

**File**: `src/Clients/nexora-portal/src/shared/lib/api.ts`
**Lines**: 18–20

**Current code**:
```ts
if (error.response?.status === 401 && typeof window !== 'undefined') {
  window.location.href = '/auth/login';
}
```

**Problem**: The 401 redirect goes to `/auth/login` — a URL that does NOT exist in this portal. All authenticated routes live under `/{locale}/...` (e.g., `/en/auth/login`, `/tr/auth/login`). Redirecting to `/auth/login` (without locale) causes a Next.js 404 or an infinite redirect loop because the `[locale]` segment is missing.

Additionally, `window.location.href` bypass triggers a full page reload and loses query state. The locale context is also lost. The user's browser language should be detected or the current locale should be read.

**Required fix**: Use the locale-prefixed path. Since this runs client-side, read the locale from the URL:
```ts
if (error.response?.status === 401 && typeof window !== 'undefined') {
  const pathSegments = window.location.pathname.split('/');
  // Detect current locale from URL (first segment after /)
  const currentLocale = ['en', 'tr'].includes(pathSegments[1]) ? pathSegments[1] : 'en';
  window.location.href = `/${currentLocale}/auth/login`;
}
```

Alternatively (and preferably), remove the redirect from `api.ts` entirely. The middleware already handles 401 redirects at the server level. For client-side 401 handling, use the `useAuth` hook's `session.error === 'RefreshAccessTokenError'` pattern which already redirects via `signOut({ callbackUrl: '/auth/login' })`.

---

## CATEGORY 2: ARCHITECTURE / NEXT.JS

### [MAJOR] ARCH-1 — `dashboard/page.tsx` and `profile/page.tsx`: Page Components Are Client Components Without Justification

**Files**:
- `src/Clients/nexora-portal/src/app/[locale]/(portal)/dashboard/page.tsx` — Line 1: `'use client';`
- `src/Clients/nexora-portal/src/app/[locale]/(portal)/profile/page.tsx` — Line 1: `'use client';`

**Problem**: Both page components are marked `'use client'`. This means they cannot be Server Components, cannot use server-side data fetching, and cannot benefit from React's streaming SSR. Per `FRONTEND_STANDARDS.md` Section 2 and Next.js App Router conventions, page components should be Server Components by default unless they require specific client-side interactivity (event handlers, browser APIs, useState/useEffect).

Looking at each file:
- `dashboard/page.tsx` uses `useTranslations()` (available server-side via `getTranslations()`), `useAuthStore` (Zustand — client only). The Zustand dependency is the ONLY reason it's a client component.
- `profile/page.tsx` uses `useTranslations()`, `useAuthStore`, `useCurrency()`, `useOrganization()`.

The problem is that user data (`user.firstName`) is read from Zustand after the `/users/me` API call completes in `useAuth()`. This creates a dependency chain: `PortalShell` → `useAuth()` → Zustand store populated → page reads from Zustand. This is an architectural issue where server data is duplicated into client state for client components to read.

**Required fix**: For the current architecture (where Zustand is the source of truth for user data), keep `'use client'` on these pages but do NOT mark them as client components unless absolutely necessary. For now, the proper fix is:

1. Remove `useAuthStore` from page components. User data should come from `useSession()` or be passed as props from the layout.
2. The layout (`PortalShell`) already has the session. Pass necessary user data down via props or use Next.js's `auth()` server-side to fetch user data in the layout and pass it as a prop.

At minimum, add a JSDoc comment explaining why `'use client'` is required:
```tsx
'use client';
// Required: useAuthStore (Zustand) and SectionRenderer need client context.
// TODO: Refactor to pass user data from server layout when useAuth sync is removed.
```

This is a MAJOR finding because it undermines the SSR benefits of Next.js App Router that were the justification for choosing Next.js over a plain React SPA (per Architecture docs).

---

### [MAJOR] ARCH-2 — `PortalLayout.tsx`: Content Area Padding Is `pt-22` — Non-Standard Tailwind Value

**File**: `src/Clients/nexora-portal/src/shared/components/layout/PortalLayout.tsx`
**Line**: 30

**Current code**:
```tsx
<main
  className={cn(
    'min-h-[calc(100vh-8rem)] p-6 pt-22 transition-all duration-300',
    sidebarOpen ? 'ml-64' : 'ml-16',
  )}
>
```

**Problem**: `pt-22` is not a standard Tailwind CSS 4 utility. The standard scale goes `pt-20` (5rem), then `pt-24` (6rem). `pt-22` will be emitted as-is if Tailwind's arbitrary value syntax is not used (`pt-[5.5rem]`), but without the bracket syntax it is likely not defined in the default Tailwind scale and will be silently ignored (no padding applied).

The topbar is `h-16` (4rem = 64px). The `p-6` (1.5rem = 24px) already applies to all sides. The correct padding-top should be `pt-20` (topbar height 4rem + padding 1rem buffer) or `pt-16` (topbar height only, letting `p-6` handle spacing). The prior review flagged the absence of `pt-16` — the fix added `pt-22` which is likely a Tailwind class that has no effect.

**Required fix**: Replace `pt-22` with a valid Tailwind value that ensures content appears below the sticky topbar:
```tsx
<main
  className={cn(
    'min-h-[calc(100vh-8rem)] p-6 pt-20 transition-all duration-300',
    sidebarOpen ? 'ml-64' : 'ml-16',
  )}
>
```

If `pt-22` is intentional for a non-standard pixel offset, use arbitrary value syntax: `pt-[5.5rem]`.

---

### [MAJOR] ARCH-3 — `PortalShell.tsx`: New Client Boundary Introduced at Wrong Level

**File**: `src/Clients/nexora-portal/src/shared/components/layout/PortalShell.tsx`
**Lines**: 1–22

**Current code**:
```tsx
'use client';

export function PortalShell({ children }: PortalShellProps) {
  useAuth();
  return <PortalLayout>{children}</PortalLayout>;
}
```

**Problem**: `PortalShell` was introduced to separate the server layout (`(portal)/layout.tsx`) from the client-side auth sync logic. This is architecturally correct in intent — the server layout does `await auth()` and then renders `<PortalShell>`. However, because `PortalShell` is `'use client'` and wraps `PortalLayout` which wraps ALL children, ANY page rendered inside the portal becomes a child of a client component tree, which means they lose the ability to be pure Server Components unless they are explicitly passed as `children` props.

In React's composition model, a Server Component CAN be rendered as a child of a Client Component IF it's passed through the `children` prop slot — which is the pattern here (`{children}` is passed through). This means the architecture is actually correct for the `children` slot. However:

1. `PortalLayout` is `'use client'` and wraps `Sidebar`, `Topbar`, `Footer` — all client components. This is correct.
2. The `children` (actual page content) passed to `PortalLayout` → `PortalShell` → `(portal)/layout.tsx` correctly preserves Server Component status.

The actual issue is: `PortalShell` calls `useAuth()` on every render. `useAuth()` triggers a `useSession()` call and potentially a `/users/me` API call. If `PortalShell` re-renders (e.g., due to session state change), this runs again. The `fetchFailed` state that guards the infinite loop is local to `useAuth()` and resets on component unmount/remount. If `PortalShell` unmounts and remounts (e.g., during route changes in certain scenarios), `fetchFailed` resets to `false` and the fetch runs again.

**Required fix**: The `fetchFailed` guard in `useAuth.ts` (line 31: `if (!user && !fetchFailed)`) already correctly prevents refetch as long as `user` is populated. However, if `/users/me` fails (network error, 403), `clearSession()` is called, setting `user` to null, which would cause the condition `!user && !fetchFailed` to be true and retry the fetch despite `fetchFailed` being true. Verify this: `clearSession()` sets `user: null` but `fetchFailed` remains `true`. Since both are checked with AND logic (`!user && !fetchFailed`), when `fetchFailed` is `true`, the fetch WON'T retry even after `clearSession()`. This is correct.

However, there is still a subtle issue: `fetchFailed` is local state in `useAuth`. If the user successfully authenticates, then the session expires, then they re-authenticate (new session, new `account`), `fetchFailed` should reset. The current code resets `fetchFailed` to `false` when `status === 'unauthenticated'` (line 49-50), which handles the re-authentication case. This is acceptable.

The real fix needed for ARCH-3 is: add a `displayName` to `PortalShell` for React DevTools clarity, and add a comment explaining why `useAuth()` is called here rather than in each page:
```tsx
PortalShell.displayName = 'PortalShell';
```

Mark this issue as MINOR upon reflection — the core architecture is sound. Reclassifying to MINOR.

**RECLASSIFIED: MINOR** — See MINOR-ARCH-1 below.

---

### [MINOR] ARCH-4 — `app/layout.tsx`: Root Layout Missing `<html>` and `<body>` Tags

**File**: `src/Clients/nexora-portal/src/app/layout.tsx`
**Lines**: 1–9

**Current code**:
```tsx
export default function RootLayout({ children }: { children: ReactNode }) {
  return children;
}
```

**Problem**: The root `app/layout.tsx` simply returns `children` without `<html>` and `<body>` tags. Next.js App Router requires the outermost layout to include `<html>` and `<body>`. In this architecture, these tags ARE in `app/[locale]/layout.tsx`. However, when Next.js resolves the layout tree, it expects the root layout to provide the document structure.

The current code works because Next.js has special handling for root layouts that delegate to locale layouts, but it is fragile. If a request does not match the `[locale]` segment (e.g., an API route that falls through, or a 404 for a top-level path like `/favicon.ico`), the root layout renders `children` without any document structure.

Per Next.js documentation, the root `app/layout.tsx` MUST render `<html>` and `<body>`. The locale layout should NOT duplicate these tags.

**Required fix**: The root layout must include `<html>` and `<body>`. The locale layout should use a fragment or a `<div>` for its wrapper. The `lang` and `dir` attributes need to be set dynamically — this requires the locale segment to be available at root layout level.

The cleanest pattern for next-intl with App Router is to have the root layout set `lang="en"` as default and let the locale layout override it. However, with next-intl, the locale layout IS the html root since it has the locale information.

At minimum, add a comment explaining the design decision:
```tsx
/**
 * Root layout — delegates html/body structure to [locale]/layout.tsx.
 * This is intentional: next-intl requires locale-aware html/body tags.
 * See: https://next-intl-docs.vercel.app/docs/getting-started/app-router
 */
export default function RootLayout({ children }: { children: ReactNode }) {
  return children;
}
```

Note: Next.js 16 with App Router technically requires `<html>` and `<body>` in the root layout. If this causes hydration warnings or errors in production, add a minimal wrapper:
```tsx
export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html suppressHydrationWarning>
      <body>{children}</body>
    </html>
  );
}
```

---

### [MINOR] ARCH-5 — `i18n/request.ts`: Flat Namespace Merge Will Not Scale (Prior MINOR Unresolved)

**File**: `src/Clients/nexora-portal/src/i18n/request.ts`
**Lines**: 12–24

**Current code**:
```ts
const [common, error, validation, navigation] = await Promise.all([
  import(`@/locales/${locale}/common.json`),
  import(`@/locales/${locale}/error.json`),
  import(`@/locales/${locale}/validation.json`),
  import(`@/locales/${locale}/navigation.json`),
]);

return {
  locale,
  messages: {
    ...common.default,
    ...error.default,
    ...validation.default,
    ...navigation.default,
  },
};
```

**Problem**: This was flagged as MINOR in the prior review and was NOT fixed. All translation keys are merged into a single flat object. This works for the current 4 namespaces (~80 keys), but when Phase 2 modules (donations.json, crm.json, sponsorship.json, etc.) are added:

1. Every page will load ALL module translations regardless of which modules are installed or which page is viewed — unnecessary payload.
2. `lockey_nav_donations` and `lockey_donations_page_title` from different files could accidentally share the same normalized key if key names are not perfectly unique.
3. next-intl supports proper namespace isolation (`useTranslations('common')`) which this pattern bypasses.

**Required fix**: Preserve namespaces in the message object:
```ts
return {
  locale,
  messages: {
    common: common.default,
    error: error.default,
    validation: validation.default,
    navigation: navigation.default,
  },
};
```

Then update all `useTranslations()` calls to use the namespace:
```tsx
const t = useTranslations('common');
// t('lockey_common_save') → key lookup within common namespace
```

Note: This is a breaking change that requires updating every `useTranslations()` call across the codebase. Schedule this refactor before Phase 2 module translations are added.

---

### [SUGGESTION] ARCH-6 — `next.config.ts`: No `IMAGE_HOSTNAME` Environment Variable in `.env.example`

**File**: `src/Clients/nexora-portal/next.config.ts`
**Line**: 11

**Current code**:
```ts
hostname: process.env.IMAGE_HOSTNAME ?? 'cdn.nexora.io',
```

**Problem**: `IMAGE_HOSTNAME` is a new environment variable introduced in the second commit's fix, but there is no `.env.example` file documenting this variable. Per `FRONTEND_STANDARDS.md` Section 15: "use `.env.example` as template." Developers will not know this variable exists without reading the source code.

**Required action**: Create `src/Clients/nexora-portal/.env.example` (if it does not exist) and document:
```env
# Next.js Image Optimization — allowed hostname for tenant logo images
# Set to your MinIO/CDN hostname. Default: cdn.nexora.io
IMAGE_HOSTNAME=cdn.nexora.io

# Keycloak OIDC Configuration
AUTH_KEYCLOAK_ID=nexora-portal
AUTH_KEYCLOAK_SECRET=your-client-secret
AUTH_KEYCLOAK_ISSUER=http://localhost:8080/realms/nexora-dev

# NextAuth.js Secret (generate with: openssl rand -base64 32)
AUTH_SECRET=your-nextauth-secret

# Backend API Base URL
NEXT_PUBLIC_API_URL=http://localhost:5000/api/v1
```

---

## CATEGORY 3: BUSINESS LOGIC

### [MAJOR] BL-1 — `auth.ts`: `AUTH_SECRET` Environment Variable Not Validated at Startup

**File**: `src/Clients/nexora-portal/src/shared/lib/auth.ts`
**Lines**: 26–33

**Current code**:
```ts
const authConfig: NextAuthConfig = {
  providers: [
    KeycloakProvider({
      clientId: process.env.AUTH_KEYCLOAK_ID!,
      clientSecret: process.env.AUTH_KEYCLOAK_SECRET!,
      issuer: process.env.AUTH_KEYCLOAK_ISSUER!,
    }),
  ],
```

**Problem**: All three environment variables (`AUTH_KEYCLOAK_ID`, `AUTH_KEYCLOAK_SECRET`, `AUTH_KEYCLOAK_ISSUER`) use the non-null assertion operator (`!`) to bypass TypeScript's undefined check. If any of these are missing at runtime (e.g., in a misconfigured deployment or a CI environment), the application will crash with a runtime error that is difficult to diagnose — or worse, NextAuth will silently use `undefined` as the client ID and fail to authenticate without a clear error message.

Additionally, NextAuth v5 requires an `AUTH_SECRET` environment variable. This is not validated anywhere. A missing `AUTH_SECRET` causes NextAuth to throw an error on the first authentication attempt, not at startup.

**Required fix**: Add environment variable validation at module load time:
```ts
function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(
      `[nexora-portal] Missing required environment variable: ${name}. ` +
      `Check .env.local and ensure all AUTH_* variables are set.`
    );
  }
  return value;
}

const authConfig: NextAuthConfig = {
  providers: [
    KeycloakProvider({
      clientId: requireEnv('AUTH_KEYCLOAK_ID'),
      clientSecret: requireEnv('AUTH_KEYCLOAK_SECRET'),
      issuer: requireEnv('AUTH_KEYCLOAK_ISSUER'),
    }),
  ],
  // ...
};
```

This ensures a clear startup error instead of a cryptic runtime failure.

---

### [MAJOR] BL-2 — `useModules.ts`: Module API Endpoint Exposes Tenant ID in URL Without Validation

**File**: `src/Clients/nexora-portal/src/shared/hooks/useModules.ts`
**Lines**: 23–29

**Current code**:
```ts
const query = useQuery({
  queryKey: moduleKeys.installed(tenantId ?? ''),
  queryFn: () =>
    api.get<TenantModuleDto[]>(
      `/identity/tenants/${tenantId}/modules`,
    ),
  enabled: !!tenantId,
  staleTime: 5 * 60 * 1000,
});
```

**Problem**: `tenantId` is interpolated directly into the URL without any sanitization or format validation. While the `enabled: !!tenantId` guard prevents the query from running when `tenantId` is null/empty, it does not prevent injection if `tenantId` contains URL-unsafe characters.

The `tenantId` comes from the Keycloak JWT `tenant_id` claim, extracted in `auth.ts` as:
```ts
token.tenantId = keycloakProfile?.tenant_id as string | undefined;
```

The `tenant_id` claim comes from Keycloak, so it should be a UUID format. However, there is no runtime type check enforcing this. If the claim is manipulated (e.g., via a compromised Keycloak configuration) or contains unexpected characters, the URL could be malformed.

**Required fix**: Validate that `tenantId` is a UUID before using it in the URL:
```ts
const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const query = useQuery({
  queryKey: moduleKeys.installed(tenantId ?? ''),
  queryFn: () =>
    api.get<TenantModuleDto[]>(`/identity/tenants/${encodeURIComponent(tenantId!)}/modules`),
  enabled: !!tenantId && UUID_REGEX.test(tenantId),
  staleTime: 5 * 60 * 1000,
});
```

Similarly apply `encodeURIComponent` in `useOrganization.ts` for `organizationId`.

---

### [MINOR] BL-3 — `useAuth.ts`: `/users/me` Fetch Error Silently Clears Session Without User Feedback

**File**: `src/Clients/nexora-portal/src/shared/hooks/useAuth.ts`
**Lines**: 41–45

**Current code**:
```ts
.catch(() => {
  setFetchFailed(true);
  clearSession();
});
```

**Problem**: This was flagged as MINOR in the prior review and was NOT fixed. When `/identity/users/me` fails (network error, 403, 404), the session is cleared silently. The user sees a blank dashboard with no navigation — `user` is null so the Topbar's user menu is hidden, the Sidebar shows only "Dashboard", and no error message is displayed.

Per `FRONTEND_STANDARDS.md` Section 9 (API Integration): "Show error toast with resolved message." The user should be informed.

**Required fix**: Show a toast error and, if appropriate, redirect to login:
```ts
.catch(() => {
  setFetchFailed(true);
  clearSession();
  // Import toast from sonner (already a dependency)
  toast.error('lockey_error_session_expired'); // Or resolve with t() if available
  // Consider: signOut({ callbackUrl: '/auth/login' });
});
```

Note: Since `useAuth` is a plain hook (not a component), it cannot use `useTranslations()`. You have two options:
1. Import `toast` from `sonner` and show the raw lockey key (not ideal but acceptable as a stopgap).
2. Accept a `t` translation function as a parameter from the calling component.
3. Better: trigger a redirect to login via `signOut({ callbackUrl: '/auth/login' })` instead of silently clearing — this is the behavior users expect when their session is invalid.

---

## CATEGORY 4: STANDARDS COMPLIANCE

### [CRITICAL] STD-1 — `Topbar.tsx`: Locale Labels Are Hardcoded English Strings

**File**: `src/Clients/nexora-portal/src/shared/components/layout/Topbar.tsx`
**Lines**: 73–76

**Current code**:
```ts
const localeLabels: Record<string, string> = {
  en: 'English',
  tr: 'Türkçe',
};
```

**Problem**: The locale display names `'English'` and `'Türkçe'` are hardcoded strings rendered in the UI. Per `LOCALIZATION_STANDARDS.md` Golden Rule: "NO hardcoded user-facing strings anywhere." These labels appear in the `<select>` dropdown rendered to the user.

While it could be argued that language names in their own script (e.g., "Türkçe" for Turkish) are correct to display in the native language, they are still hardcoded constants that violate the zero-tolerance standard. Additionally, `'English'` is a hardcoded English-language string for the locale switcher, which means non-English speakers (the intended users of the language switcher) see an English label.

**Required fix**: Add translation keys for locale names:

In `src/Clients/nexora-portal/src/locales/en/common.json`:
```json
"lockey_common_locale_en": "English",
"lockey_common_locale_tr": "Turkish"
```

In `src/Clients/nexora-portal/src/locales/tr/common.json`:
```json
"lockey_common_locale_en": "İngilizce",
"lockey_common_locale_tr": "Türkçe"
```

In `Topbar.tsx`:
```tsx
function LanguageSwitcher() {
  const t = useTranslations();
  const currentLocale = useLocale();
  // ...
  return (
    <select value={currentLocale} onChange={...}>
      {routing.locales.map((locale) => (
        <option key={locale} value={locale}>
          {t(`lockey_common_locale_${locale}`)}
        </option>
      ))}
    </select>
  );
}
```

---

### [MAJOR] STD-2 — `not-found.tsx`: Uses `useTranslations` in a Potentially Server-Less Context

**File**: `src/Clients/nexora-portal/src/app/[locale]/not-found.tsx`
**Lines**: 1–14

**Current code**:
```tsx
import { useTranslations } from 'next-intl';

export default function NotFoundPage() {
  const t = useTranslations();
  // ...
}
```

**Problem**: `not-found.tsx` is a Next.js special file for 404 pages. When rendered as a Server Component (no `'use client'` directive), `useTranslations()` from `next-intl` MUST use the server-side variant `getTranslations()` — not the hook form.

`useTranslations()` is a React hook that works in Client Components and is also supported in Server Components via next-intl's special server context. However, for `not-found.tsx` specifically, next-intl has documented limitations: the locale context may not be available in the `not-found.tsx` because it sits outside the `[locale]` layout segment in the rendering tree (Next.js can render `not-found.tsx` at the root level).

**Required fix**: Convert to async server component using `getTranslations`:
```tsx
import { getTranslations } from 'next-intl/server';

export default async function NotFoundPage() {
  const t = await getTranslations();

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-6xl font-bold text-muted-foreground">404</h1>
      <p className="text-lg text-muted-foreground">
        {t('lockey_error_not_found')}
      </p>
    </div>
  );
}
```

If `getTranslations()` fails due to missing locale context (which can happen for root-level 404s), add a try/catch with a hardcoded fallback:
```tsx
export default async function NotFoundPage() {
  let notFoundText = 'Page not found';
  try {
    const t = await getTranslations();
    notFoundText = t('lockey_error_not_found');
  } catch {
    // Locale context not available — use fallback
  }
  // ...
}
```

---

### [MINOR] STD-3 — `ErrorBoundary.tsx`: `console.error` Should Use Structured Logging Pattern

**File**: `src/Clients/nexora-portal/src/shared/components/feedback/ErrorBoundary.tsx`
**Line**: 53

**Current code**:
```ts
componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
  console.error('ErrorBoundary caught:', error, errorInfo);
}
```

**Problem**: Per `CODING_STANDARDS.md` Section 8 (Logging Standards): "Use structured logging with semantic parameters." While frontend logging is less strict than backend, `console.error` with string concatenation is explicitly discouraged. Additionally, `OBSERVABILITY_STANDARDS.md` recommends structured error reporting. In a production portal, unhandled errors should be reported to an observability system (e.g., OpenTelemetry-compatible error reporting).

The immediate concern is that string interpolation with `'ErrorBoundary caught:'` makes log aggregation harder.

**Required fix**: Use structured console.error:
```ts
componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
  console.error('[ErrorBoundary] Render error caught', { error, errorInfo });
  // TODO Phase 2: Report to observability service (OpenTelemetry)
}
```

---

### [MINOR] STD-4 — `BrandingProvider.tsx`: `isSafeUrl` Only Checks `https:` Protocol — Missing Path Traversal Check

**File**: `src/Clients/nexora-portal/src/shared/components/branding/BrandingProvider.tsx`
**Lines**: 11–18

**Current code**:
```ts
function isSafeUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'https:';
  } catch {
    return false;
  }
}
```

**Problem**: The prior MAJOR CSS injection issue was fixed by checking `protocol === 'https:'`. However, the fix is incomplete. A URL like `https://cdn.nexora.io/../../etc/passwd` or `https://cdn.nexora.io/logo.png?callback=<script>` passes the `https:` check. The CSS injection vector `url(https://evil.com/x);body{color:red}` also passes the check because the URL itself is valid HTTPS.

In CSS, the string `url(https://attacker.com/x.png)` is safe, but `url(https://attacker.com/x.png); body { display: none }` would be unsafe IF the CSS injection context is not properly escaped by the browser. However, `style.setProperty('--brand-logo-url', value)` does NOT allow CSS injection beyond the property value — the browser's CSS parser treats the entire value as the custom property value and won't allow injection into other rules. So the actual CSS injection risk is lower than originally assessed.

The remaining concern is SSRF via the Next.js image optimizer: if `organization.logoUrl` points to an internal network address (e.g., `https://169.254.169.254/latest/meta-data/` — AWS instance metadata), the `<Image>` component in `TenantLogo.tsx` would trigger a server-side request to that URL. This is partially mitigated by the `next.config.ts` `remotePatterns` restriction.

**Required fix**: Add hostname validation in `isSafeUrl` to check against a whitelist:
```ts
const ALLOWED_IMAGE_HOSTNAMES = (process.env.NEXT_PUBLIC_ALLOWED_IMAGE_HOSTNAMES ?? 'cdn.nexora.io')
  .split(',')
  .map(h => h.trim());

function isSafeUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== 'https:') return false;
    return ALLOWED_IMAGE_HOSTNAMES.some(
      (allowed) => parsed.hostname === allowed || parsed.hostname.endsWith(`.${allowed}`)
    );
  } catch {
    return false;
  }
}
```

Add `NEXT_PUBLIC_ALLOWED_IMAGE_HOSTNAMES=cdn.nexora.io` to `.env.example`.

---

## CATEGORY 5: TYPE SAFETY

### [MAJOR] TS-1 — `api.ts`: Response Data Accessed With Non-Null Assertion (`as T`) on Potentially Undefined Field

**File**: `src/Clients/nexora-portal/src/shared/lib/api.ts`
**Lines**: 46, 55, 61

**Current code**:
```ts
async get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const response = await apiClient.get<ApiEnvelope<T>>(url, { params });
  return response.data.data as T;  // <-- `as T` hides potential undefined
},
```

**Problem**: `ApiEnvelope<T>` defines `data?: T` (optional). The `response.data.data` can be `undefined` if the backend returns a response without a `data` field (e.g., 204 No Content, or certain error responses that slip through the response interceptor). Using `as T` casts `undefined` to `T`, which TypeScript accepts but is a runtime lie — any downstream code receiving `undefined` typed as `T` will fail unexpectedly.

This violates `FRONTEND_STANDARDS.md`: "TypeScript strict mode — NEVER use `any`" (and by extension, deceptive casts).

**Required fix**: Add explicit null/undefined handling:
```ts
async get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const response = await apiClient.get<ApiEnvelope<T>>(url, { params });
  const data = response.data.data;
  if (data === undefined) {
    throw new Error(`[api.get] Response envelope missing 'data' field for: ${url}`);
  }
  return data;
},
```

Apply the same pattern to `post`, `put`, and `getRaw`.

---

### [MINOR] TS-2 — `module.ts`: `PortalSection.component` Type Uses `LazyExoticComponent<ComponentType>` — Too Broad

**File**: `src/Clients/nexora-portal/src/shared/types/module.ts`
**Line**: 32

**Current code**:
```ts
component: LazyExoticComponent<ComponentType>;
```

**Problem**: `ComponentType` without a generic argument defaults to `ComponentType<object>` — any props type. This is essentially `any` for component props. A section component should be a no-argument component (takes no props since it's rendered autonomously by `SectionRenderer`). The broad type allows registering components that require props, which would cause a runtime error in `SectionRenderer` where it calls `<section.component />` with no props.

**Required fix**: Constrain to a no-props component:
```ts
component: LazyExoticComponent<() => React.ReactElement | null>;
```

Or using the React types:
```ts
component: LazyExoticComponent<React.FC>;
```

---

### [MINOR] TS-3 — `auth.ts`: `keycloakProfile` Cast Is Unsafe for `permissions` Field

**File**: `src/Clients/nexora-portal/src/shared/lib/auth.ts`
**Line**: 49

**Current code**:
```ts
token.permissions = (keycloakProfile?.permissions as string[]) ?? [];
```

**Problem**: `keycloakProfile?.permissions` is cast to `string[]` without any runtime type check. If Keycloak returns `permissions` as a string (a single permission), or as `null`, or as an array of non-strings, the downstream code (`hasPermission`, `hasAnyPermission`) will fail silently or throw.

**Required fix**: Add a runtime type check:
```ts
const rawPerms = keycloakProfile?.permissions;
token.permissions = Array.isArray(rawPerms)
  ? rawPerms.filter((p): p is string => typeof p === 'string')
  : [];
```

---

## CATEGORY 6: TESTING

### [MAJOR] TEST-1 — `RequireAuth.test.tsx`: Test Does Not Verify the `RefreshAccessTokenError` Case

**File**: `src/Clients/nexora-portal/src/shared/components/guards/RequireAuth.test.tsx`

**Problem**: `RequireAuth.tsx` handles three states from `useSession()`: `loading`, `authenticated`, `unauthenticated`. The tests cover all three. However, `useAuth.ts` has special handling for `session.error === 'RefreshAccessTokenError'` (calls `signOut`). The `RequireAuth` component itself does NOT check `session.error` — only `useAuth` does. But since `PortalShell` wraps ALL portal pages and calls `useAuth()`, a test for the refresh error path is critical.

There is no test in the new test files that verifies the `RefreshAccessTokenError` → `signOut` → redirect path in `useAuth.ts`. The `useAuth.ts` file has no test file at all.

**Required fix**: Create `src/Clients/nexora-portal/src/shared/hooks/useAuth.test.ts` with tests covering:
1. Session authenticated → `setAuthToken` called, `/users/me` fetched, `setSession` called with user data
2. Session unauthenticated → `setAuthToken(null)` called, `clearSession` called
3. `session.error === 'RefreshAccessTokenError'` → `signOut` called
4. `/users/me` fetch fails → `setFetchFailed(true)` and `clearSession` called, fetch NOT retried
5. Session changes from authenticated to unauthenticated → `fetchFailed` reset

Example structure:
```ts
// src/shared/hooks/useAuth.test.ts
vi.mock('next-auth/react', () => ({
  useSession: () => mockUseSession(),
  signOut: mockSignOut,
}));
vi.mock('@/shared/lib/api', () => ({
  api: { get: mockApiGet },
  setAuthToken: mockSetAuthToken,
}));
vi.mock('@/shared/lib/stores/authStore', () => ({
  useAuthStore: (selector) => selector(mockAuthState),
}));
```

---

### [MAJOR] TEST-2 — `useModules.test.ts`: Tests Pure Logic in Isolation — Missing TanStack Query Integration Tests

**File**: `src/Clients/nexora-portal/src/shared/hooks/useModules.test.ts`

**Problem**: The `useModules.test.ts` file tests module filtering logic as pure functions (extracted from the hook). While this demonstrates the logic is correct, it does NOT test the actual `useModules` hook. The hook:
1. Reads `tenantId` from Zustand store
2. Makes an API call with TanStack Query
3. Computes `installedModuleNames` and `activeModules` from query data
4. Provides `hasModule` callback

None of this is tested. The filtering logic tests are valuable but incomplete. This approach tests an extracted copy of the logic, not the actual hook.

**Required fix**: Add a proper hook test using `renderHook` from React Testing Library:
```ts
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClientWrapper } from '@/test/utils'; // create this wrapper

// Test that the hook calls the API and returns filtered modules
it('should fetch and filter installed modules for current tenant', async () => {
  mockApiGet.mockResolvedValue([
    { moduleName: 'donations', isActive: true },
    { moduleName: 'crm', isActive: false },
  ]);

  const { result } = renderHook(() => useModules(), {
    wrapper: QueryClientWrapper,
  });

  await waitFor(() => expect(result.current.isLoading).toBe(false));
  expect(result.current.hasModule('donations')).toBe(true);
  expect(result.current.hasModule('crm')).toBe(false);
});
```

---

### [MAJOR] TEST-3 — `SectionRenderer.test.tsx`: Test Uses `as never` Cast — Incorrect Type Safety in Tests

**File**: `src/Clients/nexora-portal/src/shared/components/layout/SectionRenderer.test.tsx`
**Lines**: 23–29

**Current code**:
```ts
function createTestComponent(text: string) {
  return lazy(
    () =>
      new Promise((resolve) => {
        resolve({
          default: () => <div>{text}</div>,
        } as never);
      }),
  );
}
```

**Problem**: `as never` is used to bypass TypeScript's type checking on the Promise resolution value. This is a test quality issue — tests should be type-safe. The `as never` cast means any typo or structural mismatch in the resolved value will not be caught by TypeScript. Per `CODING_STANDARDS.md`: "TypeScript strict mode — NEVER use `any`" (and by extension `as never` is an even more aggressive escape hatch).

**Required fix**: Use proper typing:
```ts
function createTestComponent(text: string): LazyExoticComponent<React.FC> {
  return lazy(
    async () => ({
      default: function TestComponent() {
        return <div>{text}</div>;
      },
    }),
  );
}
```

---

### [MINOR] TEST-4 — `RequireAuth.test.tsx`: No Test for Custom Fallback When Unauthenticated

**File**: `src/Clients/nexora-portal/src/shared/components/guards/RequireAuth.test.tsx`

**Problem**: `RequireAuth` has a `fallback` prop that is currently only tested for the `loading` state. There is no test for `fallback` when the session is `unauthenticated`. While the current implementation redirects immediately when unauthenticated (renders `null`), the test should confirm that no children are rendered.

This is a minor gap — the unauthenticated redirect IS tested (line 51–62), but the test only checks that `mockReplace` was called, not that content is hidden. If the implementation changes to show a fallback instead of redirecting when unauthenticated, the test would pass incorrectly.

**Required fix**: Add assertion for content not being rendered AND add a test for the redirect to verify that it does NOT render children:
```ts
it('should not render children when unauthenticated', () => {
  mockUseSession.mockReturnValue({ status: 'unauthenticated' });
  render(
    <RequireAuth>
      <div data-testid="protected">Protected Content</div>
    </RequireAuth>,
  );
  expect(screen.queryByTestId('protected')).not.toBeInTheDocument();
});
```

---

### [MINOR] TEST-5 — `currency.test.ts`: Negative Amount Test Uses `toBe` But the Format Is Locale-Dependent

**File**: `src/Clients/nexora-portal/src/shared/lib/currency.test.ts`
**Lines**: 27–29

**Problem**: This was flagged as MINOR in the prior review and remains partially unaddressed. The current test:
```ts
it('should handle negative amounts', () => {
  const result = formatMoney({ amount: -50, currency: 'USD' });
  expect(result).toBe('-$50.00');
});
```

Uses `toBe('-$50.00')` which will fail on Node.js versions or platforms where `Intl.NumberFormat` formats negative currency differently (e.g., `($50.00)` on some Windows locales, `−$50.00` with a Unicode minus sign on some versions). The fix from the prior review was implemented correctly with `toBe` instead of `toContain`, but the actual value depends on the runtime's `Intl` implementation.

**Required fix**: Make the test environment-independent by normalizing the output:
```ts
it('should handle negative amounts', () => {
  const result = formatMoney({ amount: -50, currency: 'USD' });
  // Normalize unicode minus to ASCII minus for comparison
  const normalized = result.replace(/\u2212/g, '-');
  expect(normalized).toBe('-$50.00');
});
```

Or use `toMatch` with a regex that accepts both formats:
```ts
expect(result).toMatch(/^[-−\(]\$50\.00\)?$/);
```

---

### [SUGGESTION] TEST-6 — `api.ts` Has No Tests

**File**: `src/Clients/nexora-portal/src/shared/lib/api.ts`

The `extractApiError` function and the API client's 401 redirect behavior have no test coverage. Per `FRONTEND_STANDARDS.md` Section 11.4: "Minimum: All shared components + hooks." The `api.ts` module is critical shared infrastructure.

**Required tests**:
1. `extractApiError` → returns `lockey_error_unexpected` for non-Axios errors
2. `extractApiError` → returns envelope `message` for Axios errors with response data
3. `extractApiError` → returns correct `status` code
4. `api.get` → unwraps `data` from envelope
5. `api.get` → throws if `data` is undefined (after TS-1 fix)
6. 401 response → triggers redirect (mock `window.location.href`)

---

## CATEGORY 7: i18n / LOCALIZATION

### [CRITICAL — reclassified from STD-1] i18n-1 — `Topbar.tsx`: Hardcoded Language Names (See STD-1)

Already documented above as STD-1. Reclassifying as CRITICAL because it is a zero-tolerance LOCALIZATION_STANDARDS violation: user-facing strings rendered in the UI must use `lockey_` keys.

---

### [MAJOR] i18n-2 — `locales/en/common.json` and `locales/tr/common.json`: Missing Keys Used in Code

**Files**:
- `src/Clients/nexora-portal/src/locales/en/common.json`
- `src/Clients/nexora-portal/src/locales/tr/common.json`

**Problem**: Cross-referencing all `t('lockey_...')` calls in the codebase against the translation files reveals the following key used in code but MISSING from the translation files:

1. **`lockey_common_locale_en`** — used in the fix for STD-1 (not yet added)
2. **`lockey_common_locale_tr`** — used in the fix for STD-1 (not yet added)

Additionally, checking `navigation.json` vs code usage:
- The `lockey_nav_profile` key is present in `navigation.json` and used in `profile/page.tsx` — CORRECT.
- The `lockey_common_app_name` value is `"Nexora Portal"` — this is fine as a product name.

The following keys are present in the translation files but appear to have no current usage in the codebase (dead keys — not a violation but worth noting):
- `lockey_common_confirm`
- `lockey_common_delete`
- `lockey_common_edit`
- `lockey_common_back`
- `lockey_common_search`
- `lockey_common_settings`
- `lockey_common_profile`
- `lockey_common_module_not_available`
- `lockey_nav_donations`, `lockey_nav_sponsorships`, `lockey_nav_events`, `lockey_nav_documents`, `lockey_nav_education`, `lockey_nav_surveys` (these are for future modules — fine to have preemptively)

**Required fix**: After implementing the STD-1 fix (locale label keys), add to BOTH `en/common.json` and `tr/common.json`:

English (`en/common.json`):
```json
"lockey_common_locale_en": "English",
"lockey_common_locale_tr": "Turkish"
```

Turkish (`tr/common.json`):
```json
"lockey_common_locale_en": "İngilizce",
"lockey_common_locale_tr": "Türkçe"
```

---

### [MINOR] i18n-3 — `globals.css`: Dark Mode Uses `@media prefers-color-scheme` But No User-Toggleable Dark Mode State

**File**: `src/Clients/nexora-portal/src/app/globals.css`
**Lines**: 47–64

**Problem**: The CSS implements dark mode via `@media (prefers-color-scheme: dark)`, which responds only to the OS-level setting. The `uiStore.ts` has no `theme` state for user-controlled dark/light toggle. This means:
1. Users cannot override their OS dark mode preference within the portal.
2. If the portal serves an organization that requires light mode (e.g., for accessibility reasons), there is no way to enforce it.

The prior review flagged this as an INFO item. Raising to MINOR because `globals.css` now contains dark mode rules that have no associated UI control, creating an incomplete feature.

**Required fix**: Either:
- Add a `theme: 'light' | 'dark' | 'system'` state to `uiStore.ts` and apply a `dark` class to `<html>` based on it (replacing the media query approach), OR
- Remove the media query dark mode rules from `globals.css` and document that dark mode support is a future phase feature.

The current state (CSS dark mode with no user control) is technically functional (OS preference respected) but inconsistent with having a UI store for UI state.

---

## CATEGORY 8: PERFORMANCE

### [MAJOR] PERF-1 — `PortalLayout.tsx` and `Footer.tsx`: Both Components Subscribe to Full `sidebarOpen` State Separately

**Files**:
- `src/Clients/nexora-portal/src/shared/components/layout/PortalLayout.tsx` — Line 21: `const sidebarOpen = useUiStore((s) => s.sidebarOpen);`
- `src/Clients/nexora-portal/src/shared/components/layout/Topbar.tsx` — Line 18: `const sidebarOpen = useUiStore((s) => s.sidebarOpen);`
- `src/Clients/nexora-portal/src/shared/components/layout/Footer.tsx` — Line 10: `const sidebarOpen = useUiStore((s) => s.sidebarOpen);`

**Problem**: Three separate components each subscribe to `sidebarOpen` from the Zustand store. When the sidebar toggles, all three components re-render. While Zustand's selector pattern (`useUiStore((s) => s.sidebarOpen)`) is correct and each component only re-renders when `sidebarOpen` changes (not on any store update), the structural pattern is problematic:

`Footer`, `Topbar`, and `main` in `PortalLayout` all have `ml-64` / `ml-16` classes driven by `sidebarOpen`. This means the sidebar width is hardcoded in three different places. If the sidebar width changes (e.g., to 72px collapsed instead of 64px), three files need updating.

Per `CODING_STANDARDS.md`: "DRY — but only when duplication is true duplication (same reason to change)." These three margin adjustments change for the same reason — sidebar width change.

**Required fix**: Centralize the margin class logic. Use CSS variables for sidebar width:

In `globals.css`:
```css
:root {
  --sidebar-width-open: 16rem;   /* 64 * 4 = 256px */
  --sidebar-width-closed: 4rem;  /* 16 * 4 = 64px */
}
```

In `PortalLayout.tsx`, pass margin as a CSS variable rather than switching Tailwind classes:
```tsx
<main
  style={{ marginLeft: sidebarOpen ? 'var(--sidebar-width-open)' : 'var(--sidebar-width-closed)' }}
  className="min-h-[calc(100vh-8rem)] p-6 pt-20 transition-all duration-300"
>
```

Or alternatively, keep the Tailwind approach but extract the margin class computation into a shared utility:
```ts
// shared/lib/layout.ts
export const sidebarMargin = (open: boolean) => open ? 'ml-64' : 'ml-16';
```

---

### [MINOR] PERF-2 — `Sidebar.tsx`: `allNavItems` Array Reconstructed on Every Render

**File**: `src/Clients/nexora-portal/src/shared/components/layout/Sidebar.tsx`
**Lines**: 43–49

**Current code**:
```ts
const allNavItems: PortalNavigationItem[] = [
  { label: 'lockey_nav_dashboard', path: '/dashboard', icon: 'Home' },
  ...activeModules.flatMap((m) => {
    const canAccessModule = m.permissions.some((p) => hasPermission(p));
    return canAccessModule ? m.navigation : [];
  }),
];
```

**Problem**: `allNavItems` is constructed inline without memoization. On each render of `Sidebar`, a new array is created with `flatMap`, `some`, and object spreading. While each individual re-render is fast, `Sidebar` re-renders every time `activeModules`, `hasPermission`, or the component's own state changes.

`hasPermission` is a function from the Zustand store — it changes reference on every store update because Zustand recreates it as part of the state object.

**Required fix**: Memoize `allNavItems`:
```ts
const allNavItems = useMemo(
  () => [
    { label: 'lockey_nav_dashboard', path: '/dashboard', icon: 'Home' },
    ...activeModules.flatMap((m) => {
      const canAccessModule = m.permissions.some((p) => hasPermission(p));
      return canAccessModule ? m.navigation : [];
    }),
  ],
  [activeModules, hasPermission],
);
```

Note: `hasPermission` from `usePermissions()` should itself be stable (it reads from Zustand store via closure). Check that `usePermissions` returns a stable reference — it currently does because it's destructured from the store state, not a new function.

---

### [SUGGESTION] PERF-3 — `query.ts`: `refetchOnWindowFocus: false` Globally May Be Too Aggressive

**File**: `src/Clients/nexora-portal/src/shared/lib/query.ts`
**Line**: 9

**Current code**:
```ts
refetchOnWindowFocus: false,
```

**Problem**: Disabling `refetchOnWindowFocus` globally means that when a user switches tabs and returns, stale data is never automatically refreshed. For the portal's use case (donor data, sponsorship statuses, event registrations), returning to the portal after checking email should show current data. The `staleTime: 30 * 1000` (30 seconds) means queries older than 30s won't refetch on window focus — but disabling it entirely means they NEVER will.

Per `API_INTEGRATION_GUIDE.md` Section 6.1: `refetchOnWindowFocus: false` is recommended to prevent "aggressive refetching." This is a standards-compliant choice. However, it should be reconsidered for portal use cases where data freshness matters more than for admin dashboards.

This is a SUGGESTION, not a violation, since it matches the standards doc.

---

## CATEGORY 9: ERROR HANDLING

### [MAJOR] ERR-1 — `(portal)/layout.tsx`: Auth Failure Does Not Pass Locale to Redirect

**File**: `src/Clients/nexora-portal/src/app/[locale]/(portal)/layout.tsx`
**Lines**: 21–25

**Current code**:
```ts
const session = await auth();
const { locale } = await params;

if (!session) {
  redirect({ href: '/auth/login', locale });
}
```

**Problem**: This is actually correct — locale IS passed to the redirect. However, there is a subtle timing issue: `auth()` is awaited before `params` is awaited. If `auth()` throws an unexpected error (e.g., NextAuth configuration error, database unavailability), the error is not caught and propagates as an unhandled server error, which Next.js renders as a 500 page rather than a proper error boundary redirect.

More critically: `redirect()` from next-intl throws a `NEXT_REDIRECT` exception internally. If `redirect()` is called inside a `try/catch`, the redirect is caught and swallowed. This is NOT the case here, but it's a footgun to be aware of.

The actual issue is: what happens when `session` exists but `session.error === 'RefreshAccessTokenError'`? The server layout only checks `if (!session)`. A valid session object with `error: 'RefreshAccessTokenError'` passes the null check and renders the portal — but the tokens are expired. The `useAuth()` hook in `PortalShell` catches this client-side via `signOut()`, but there is a window where the portal renders with expired tokens.

**Required fix**: Add error state check to the server layout:
```ts
export default async function PortalRouteLayout({ children, params }: PortalRouteLayoutProps) {
  const session = await auth();
  const { locale } = await params;

  if (!session || session.error === 'RefreshAccessTokenError') {
    redirect({ href: '/auth/login', locale });
  }

  return <PortalShell>{children}</PortalShell>;
}
```

---

### [MAJOR] ERR-2 — `SectionRenderer.tsx`: No Error Boundary Around Individual Sections

**File**: `src/Clients/nexora-portal/src/shared/components/layout/SectionRenderer.tsx`
**Lines**: 42–50

**Current code**:
```tsx
{sections.map((section) => (
  <Suspense
    key={section.id}
    fallback={<LoadingSkeleton className="h-32" />}
  >
    <section.component />
  </Suspense>
))}
```

**Problem**: Each section is wrapped in `<Suspense>` for lazy loading, but NOT in an `<ErrorBoundary>`. If a module's section component throws a render error (e.g., a newly installed module's component has a bug), the entire `SectionRenderer` crashes, taking down the dashboard/profile page.

Per `FRONTEND_STANDARDS.md` Section 5.2: "Error boundaries at route level, not per component." However, for the page builder infrastructure where sections come from different modules, per-section error isolation is critical — a bug in the `donations` section should not break the `events` section.

**Required fix**: Wrap each section in both `Suspense` and `ErrorBoundary`:
```tsx
import { ErrorBoundary } from '@/shared/components/feedback/ErrorBoundary';

{sections.map((section) => (
  <ErrorBoundary key={section.id}>
    <Suspense fallback={<LoadingSkeleton className="h-32" />}>
      <section.component />
    </Suspense>
  </ErrorBoundary>
))}
```

---

### [MINOR] ERR-3 — `useOrganization.ts`: `isLoading` vs `isPending` — Wrong TanStack Query v5 State

**File**: `src/Clients/nexora-portal/src/shared/hooks/useOrganization.ts`
**Line**: 31

**Current code**:
```ts
return {
  organization: query.data ?? null,
  isLoading: query.isLoading,
};
```

**Problem**: In TanStack Query v5, `isLoading` is `true` only when the query is both in `pending` state AND has no cached data. In v5, `isLoading` was renamed to `isPending` for new queries. If you use `isLoading` on a query that previously loaded data (cache hit), it returns `false` even if the background refetch is in progress. For the `BrandingProvider` use case, this means `organization` could be `null` while branding data is being loaded on cold start.

The same issue exists in `useModules.ts` (line 54: `isLoading: query.isLoading`).

**Required fix** in both files:
```ts
return {
  organization: query.data ?? null,
  isLoading: query.isPending, // v5: use isPending for "first load" semantics
};
```

---

## CATEGORY 10: CODE QUALITY

### [MINOR] CQ-1 — `Topbar.tsx`: `signOut` Redirect URL Is Locale-Unaware

**File**: `src/Clients/nexora-portal/src/shared/components/layout/Topbar.tsx`
**Line**: 54

**Current code**:
```tsx
onClick={() => signOut({ callbackUrl: '/' })}
```

**Problem**: `callbackUrl: '/'` redirects to the root URL after sign-out. The root URL `/` does not exist in this portal — all routes are under `/{locale}/`. After sign-out, the user hits the root, which triggers the `middleware.ts` redirect to `/{locale}/auth/login`. However, this creates an unnecessary double redirect (/ → /en/auth/login).

**Required fix**: Use a locale-aware callback URL:
```tsx
onClick={() => signOut({ callbackUrl: `/${currentLocale}/auth/login` })}
```

Where `currentLocale` is already available from `useLocale()` (used in `LanguageSwitcher`). Extract `useLocale()` to the `Topbar` component level.

---

### [MINOR] CQ-2 — `globals.css`: CSS Custom Property `--radius` Is Defined but References Itself (Circular Reference)

**File**: `src/Clients/nexora-portal/src/app/globals.css`
**Lines**: 17 and 42

**Current code**:
```css
:root {
  --radius: 0.5rem;
  /* ... */
}

@theme inline {
  /* ... */
  --radius: var(--radius);  /* ← circular self-reference! */
}
```

**Problem**: In `@theme inline` (Tailwind CSS v4 syntax), `--radius: var(--radius)` creates a circular CSS custom property reference. This causes the `--radius` variable to resolve to `undefined` (empty) in Tailwind's theme processing context. Any Tailwind class using `rounded-*` that depends on `--radius` will get unexpected values.

**Required fix**: Remove the self-referential `--radius` assignment from `@theme inline`, or use a distinct name:
```css
@theme inline {
  --radius-default: var(--radius);  /* map to Tailwind's radius system */
}
```

Or simply remove the `--radius` line from `@theme inline` since Tailwind v4 uses direct CSS variables for border-radius.

---

### [MINOR] CQ-3 — `package.json`: `dev` Script Uses `next dev` Without Turbopack Flag

**File**: `src/Clients/nexora-portal/package.json`
**Line**: 6

**Current code**:
```json
"dev": "next dev",
```

**Problem**: Per `FRONTEND_STANDARDS.md` Section 2, the Portal build tool is "Turbopack (Next.js built-in)." In Next.js 16, Turbopack is the default for development (`next dev` uses Turbopack by default in Next.js 15+). However, explicitly documenting the flag makes the intent clear and ensures CI/CD pipelines do not accidentally use Webpack:

```json
"dev": "next dev --turbopack",
```

This is a minor documentation/explicitness issue, not a functional bug.

---

### [SUGGESTION] CQ-4 — `_registry.ts`: Module Registration Pattern Should Document How to Add a Module

**File**: `src/Clients/nexora-portal/src/modules/_registry.ts`

The registry comment says:
```ts
// Module manifests will be added here as portal modules are built.
// Example:
// donationsManifest,
```

This is helpful but insufficient. The comment should reference where the manifest file should be created and what the `PortalModuleManifest` type requires. Consider adding a link to the module system docs.

**Required fix**: Expand the comment:
```ts
/**
 * Portal Module Registry
 *
 * Add module manifests here as portal modules are implemented.
 * Each entry must implement PortalModuleManifest (src/shared/types/module.ts).
 *
 * Pattern:
 * 1. Create: src/modules/{moduleName}/manifest.ts
 * 2. Export: export const {moduleName}Manifest: PortalModuleManifest = { ... }
 * 3. Register: import here and add to allPortalModules array
 *
 * See: docs/architecture/MODULE_SYSTEM.md Section 7 (Portal UI Integration)
 */
```

---

### [SUGGESTION] CQ-5 — `tsconfig.json` Should Be Verified for `strict: true`

**Note**: The `tsconfig.json` was listed as a reviewed file but was NOT read during this review pass. The prior review noted it has `"strict": true`. Verify that the final `tsconfig.json` (after all fixes) still has:
```json
{
  "compilerOptions": {
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true
  }
}
```

`noUncheckedIndexedAccess` would catch the `parts[1]` access in the middleware (SEC-1) at compile time.

---

## PRIOR REVIEW ITEMS: STATUS CHECK

All items from the prior review (`2026-03-21-portal-framework-phase1-5-review.md`) that were supposed to be fixed by commit `c7bbab0e` were verified as resolved, EXCEPT:

| Prior Item | Expected Fix | Actual Status |
|-----------|-------------|---------------|
| MINOR 5.3 — `useAuth.ts` silent error catch | Show toast or redirect | NOT FIXED — `.catch(() => { setFetchFailed(true); clearSession(); })` — now documented as BL-3 |
| MINOR 6.3 — currency test negative amount | Use `toBe('-$50.00')` | PARTIALLY FIXED — uses `toBe` but value is runtime-dependent — see TEST-5 |
| MINOR 3.4 — `i18n/request.ts` flat namespace merge | Use namespace-keyed messages | NOT FIXED — same flat merge pattern — see ARCH-5 |
| MAJOR 4.1 — `PortalLayout.tsx` pt-16 missing | Add `pt-16` class | PARTIALLY FIXED — added `pt-22` which is NOT a valid Tailwind class — see ARCH-2 |

---

## VERIFICATION CHECKLIST

After applying all fixes, you MUST verify the following before marking this review complete:

### Security Verification
- [ ] `middleware.ts`: Locale extracted from URL is validated against `routing.locales` list
- [ ] `api.ts`: 401 redirect uses locale-prefixed URL (`/{locale}/auth/login`)
- [ ] `auth.ts`: All three `AUTH_*` env vars validated at startup with `requireEnv()`
- [ ] `BrandingProvider.tsx`: `isSafeUrl` validates against an allowed hostname list
- [ ] `(portal)/layout.tsx`: `session.error === 'RefreshAccessTokenError'` triggers redirect

### Architecture Verification
- [ ] `dashboard/page.tsx` and `profile/page.tsx`: Either converted to Server Components or have explanatory comments for `'use client'`
- [ ] `PortalLayout.tsx`: `pt-22` replaced with valid `pt-20` or `pt-[5.5rem]`
- [ ] `app/layout.tsx`: Has explanatory comment or valid `<html>`/`<body>` fallback
- [ ] `i18n/request.ts`: Namespace-keyed messages (or a technical debt comment with Phase 2 refactor plan)

### Standards Compliance Verification
- [ ] `Topbar.tsx`: Locale labels use `t('lockey_common_locale_en')` etc. — no hardcoded strings
- [ ] `not-found.tsx`: Uses `getTranslations()` (server-side) not `useTranslations()` hook
- [ ] `ErrorBoundary.tsx`: `console.error` uses structured format
- [ ] All translation files: `lockey_common_locale_en` and `lockey_common_locale_tr` present in both `en/` and `tr/`

### Type Safety Verification
- [ ] `api.ts`: `response.data.data as T` replaced with null check + explicit error
- [ ] `module.ts`: `component: LazyExoticComponent<React.FC>` (no-props constraint)
- [ ] `auth.ts`: `permissions` array validated as `string[]` with `Array.isArray()` check
- [ ] `SectionRenderer.test.tsx`: `as never` replaced with proper `LazyExoticComponent<React.FC>` type

### Testing Verification
- [ ] `useAuth.test.ts` exists with tests for: authenticated sync, unauthenticated clear, RefreshAccessTokenError signOut, fetch failure, reset on re-auth
- [ ] `useModules.test.ts`: Replaced with or supplemented by actual hook test using `renderHook`
- [ ] `SectionRenderer.test.tsx`: `as never` removed
- [ ] `api.test.ts` exists with tests for: envelope unwrapping, null data handling, 401 redirect
- [ ] `RequireAuth.test.tsx`: Assertion added that children are not rendered when unauthenticated

### i18n Verification
- [ ] Run: `grep -r '"[A-Z]' src/Clients/nexora-portal/src/shared/components/ src/Clients/nexora-portal/src/app/` — no hardcoded English strings in JSX
- [ ] `lockey_common_locale_en` and `lockey_common_locale_tr` present in both locale files
- [ ] All keys present in `en/` are also present in `tr/` and vice versa (parity check)
- [ ] `not-found.tsx` correctly uses server-side translation

### Business Logic Verification
- [ ] `useModules.ts`: `tenantId` validated as UUID before URL interpolation
- [ ] `useOrganization.ts`: `organizationId` URL-encoded with `encodeURIComponent`
- [ ] `(portal)/layout.tsx`: `session.error` check added before rendering portal

### Performance Verification
- [ ] `globals.css`: Circular `--radius: var(--radius)` in `@theme inline` removed
- [ ] `Sidebar.tsx`: `allNavItems` wrapped in `useMemo`
- [ ] `useOrganization.ts` and `useModules.ts`: `isLoading` changed to `isPending`

### Error Handling Verification
- [ ] `SectionRenderer.tsx`: Each section wrapped in `<ErrorBoundary>`
- [ ] `useAuth.ts`: `/users/me` failure shows toast or redirects to login

### Code Quality Verification
- [ ] `Topbar.tsx`: `signOut` uses locale-aware `callbackUrl`
- [ ] `package.json`: `dev` script uses `--turbopack` flag
- [ ] `.env.example` exists and documents all `AUTH_*` and `IMAGE_HOSTNAME` variables

### Build Verification
- [ ] `npm run build` completes with 0 TypeScript errors
- [ ] `npm run test` passes all tests (previously 35 tests, more after new tests added)
- [ ] `npm run lint` passes with 0 ESLint errors

---

## Appendix: Files That Were Correctly Implemented (No Action Required)

The following files are well-implemented and require no changes:

| File | Assessment |
|------|-----------|
| `src/shared/lib/auth.ts` | JWT manual decode removed, refresh error properly propagated, session callback correct |
| `src/shared/lib/query.ts` | SSR-safe factory pattern, correct defaults |
| `src/shared/lib/utils.ts` | `cn()` utility — correct `clsx` + `tailwind-merge` implementation |
| `src/shared/lib/currency.ts` | `Intl.NumberFormat` — correct implementation |
| `src/shared/lib/stores/authStore.ts` | Zustand pattern correct, `hasPermission`/`hasAnyPermission` correct |
| `src/shared/lib/stores/uiStore.ts` | Minimal — correct |
| `src/shared/types/api.ts` | Matches backend `ApiEnvelope<T>` contract exactly |
| `src/shared/types/auth.ts` | Type definitions match API contract |
| `src/shared/hooks/usePermissions.ts` | Thin wrapper — correct |
| `src/shared/hooks/useCurrency.ts` | Organization currency + locale — correct |
| `src/shared/hooks/useDirection.ts` | RTL locale set — correct |
| `src/shared/components/guards/RequirePermission.tsx` | `mode: 'all' \| 'any'` — correct |
| `src/shared/components/feedback/LoadingSkeleton.tsx` | Correct skeleton pattern |
| `src/shared/components/branding/TenantLogo.tsx` | Skeleton loading, initials fallback — correct |
| `src/shared/components/layout/PortalShell.tsx` | Server/client separation intent correct |
| `src/shared/components/layout/Sidebar.tsx` | Filter logic fixed, module-aware nav — correct |
| `src/app/[locale]/layout.tsx` | Locale layout with `html`/`body`, `dir` detection — correct |
| `src/app/[locale]/providers.tsx` | SessionProvider → QueryClient → BrandingProvider order correct |
| `src/app/[locale]/page.tsx` | Locale-aware redirect — correct |
| `src/app/[locale]/auth/login/page.tsx` | Keycloak OIDC sign-in flow — correct |
| `src/app/api/auth/[...nextauth]/route.ts` | Minimal handler — correct |
| `src/i18n/routing.ts` | Locale list matches translation files |
| `src/i18n/navigation.ts` | `createNavigation` exports — correct |
| `src/modules/_registry.ts` | Empty registry — correct for Phase 1.5 |
| `vitest.config.ts` | Coverage config, jsdom, path aliases — correct |
| `eslint.config.mjs` | next/core-web-vitals + typescript — correct |
| `postcss.config.mjs` | Tailwind CSS 4 PostCSS — correct |
| `src/locales/en/common.json` | All keys present (except locale labels added by STD-1 fix) |
| `src/locales/tr/common.json` | Parity with `en` — correct |
| `src/locales/en/error.json` | All error keys present |
| `src/locales/tr/error.json` | Parity with `en` — correct |
| `src/locales/en/navigation.json` | Nav keys present |
| `src/locales/tr/navigation.json` | Parity with `en` — correct |
| `src/locales/en/validation.json` | Validation keys present |
| `src/locales/tr/validation.json` | Parity with `en` — correct |
| `src/shared/lib/stores/authStore.test.ts` | All 6 store scenarios covered |
| `src/shared/lib/currency.test.ts` | 9 test cases — adequate (minor test robustness issue in TEST-5) |
| `src/shared/components/guards/RequireAuth.test.tsx` | 4 tests — covers main paths |
| `src/shared/components/guards/RequirePermission.test.tsx` | 5 tests — covers all/any modes |
| `src/shared/components/layout/SectionRenderer.test.tsx` | 4 tests — covers position/permission/order |
