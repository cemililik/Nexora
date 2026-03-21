# Code Review: Portal Framework — Phase 1.5

**Date**: 2026-03-21
**Reviewer**: AI Agent (Claude Sonnet 4.6)
**Commit**: `b0a82893` — `feat(portal): implement Phase 1.5 Portal Framework with Next.js 16`
**Branch**: `development`
**Scope**: Tüm yeni dosyalar — 65 dosya, ~1800 satır yeni kod
**Standards Referenced**: `FRONTEND_STANDARDS.md`, `LOCALIZATION_STANDARDS.md`, `CODING_STANDARDS.md`, `API_INTEGRATION_GUIDE.md`, `MODULE_SYSTEM.md`, `ARCHITECTURE/OVERVIEW.md`, `CLAUDE.md`

---

## Review Summary

| Kategori | Bulgu | Critical | Major | Minor | Info |
|----------|-------|----------|-------|-------|------|
| Güvenlik | 4 | 3 | 1 | 0 | 0 |
| Localization Standards | 2 | 2 | 0 | 0 | 0 |
| Mimari & Next.js Patterns | 4 | 0 | 3 | 1 | 0 |
| Layout & UI Bug | 2 | 0 | 2 | 0 | 0 |
| State Management & Hooks | 4 | 0 | 1 | 2 | 1 |
| Test Coverage | 3 | 0 | 2 | 1 | 0 |
| Configuration | 2 | 0 | 1 | 1 | 0 |
| Kod Kalitesi | 3 | 0 | 0 | 2 | 1 |
| **Toplam** | **24** | **5** | **10** | **7** | **2** |

**Verdict**: `CHANGES_REQUESTED` — 5 critical ve 10 major bulgu merge öncesi mutlaka düzeltilmelidir.

---

## Severity Legend

- **CRITICAL**: Güvenlik açığı, veri bütünlüğü riski, deployment blocker veya sıfır toleranslı standard ihlali — merge blocker
- **MAJOR**: Mimari sorun, business logic hatası veya standarttan belirgin sapma — merge öncesi düzeltilmeli
- **MINOR**: Kod kalitesi sorunu veya minor standart sapması — düzeltilmeli ama merge blocker değil
- **INFO**: Öneri, gözlem veya best practice notu — opsiyonel

---

## Değişen Dosyalar

| # | Dosya | Tür | Amaç |
|---|-------|-----|------|
| 1 | `src/app/layout.tsx` | Yeni | Root layout (minimal shell) |
| 2 | `src/app/globals.css` | Yeni | Tailwind CSS 4, CSS custom properties, dark mode |
| 3 | `src/app/[locale]/layout.tsx` | Yeni | Locale-aware layout (html/body, NextIntlClientProvider) |
| 4 | `src/app/[locale]/providers.tsx` | Yeni | Client providers (SessionProvider, QueryClient, BrandingProvider) |
| 5 | `src/app/[locale]/page.tsx` | Yeni | Root redirect → dashboard |
| 6 | `src/app/[locale]/not-found.tsx` | Yeni | Locale-aware 404 sayfası |
| 7 | `src/app/[locale]/(portal)/layout.tsx` | Yeni | Authenticated portal layout |
| 8 | `src/app/[locale]/(portal)/dashboard/page.tsx` | Yeni | Dashboard sayfası |
| 9 | `src/app/[locale]/(portal)/profile/page.tsx` | Yeni | Profil sayfası |
| 10 | `src/app/[locale]/auth/login/page.tsx` | Yeni | Login sayfası |
| 11 | `src/app/api/auth/[...nextauth]/route.ts` | Yeni | NextAuth.js API route |
| 12 | `src/i18n/routing.ts` | Yeni | next-intl locale routing tanımı |
| 13 | `src/i18n/request.ts` | Yeni | Server-side mesaj yükleme |
| 14 | `src/i18n/navigation.ts` | Yeni | next-intl navigation helpers |
| 15 | `src/middleware.ts` | Yeni | next-intl middleware (i18n routing) |
| 16 | `src/modules/_registry.ts` | Yeni | Portal modül registry (şimdilik boş) |
| 17 | `src/shared/lib/api.ts` | Yeni | Axios tabanlı API client |
| 18 | `src/shared/lib/auth.ts` | Yeni | NextAuth.js v5 konfigürasyonu (Keycloak) |
| 19 | `src/shared/lib/query.ts` | Yeni | TanStack Query client factory |
| 20 | `src/shared/lib/utils.ts` | Yeni | `cn()` utility (clsx + tailwind-merge) |
| 21 | `src/shared/lib/currency.ts` | Yeni | `formatMoney`, `getCurrencySymbol` |
| 22 | `src/shared/lib/stores/authStore.ts` | Yeni | Zustand auth store |
| 23 | `src/shared/lib/stores/uiStore.ts` | Yeni | Zustand UI store (sidebar) |
| 24 | `src/shared/types/api.ts` | Yeni | `ApiEnvelope<T>`, `PagedResult<T>`, `PaginationParams` |
| 25 | `src/shared/types/auth.ts` | Yeni | `UserInfo`, `OrganizationBranding`, `JwtClaims`, `PortalSession` |
| 26 | `src/shared/types/module.ts` | Yeni | `PortalModuleManifest`, `PortalSection`, `TenantModuleDto` |
| 27 | `src/shared/hooks/useAuth.ts` | Yeni | NextAuth session → Zustand sync + /users/me fetch |
| 28 | `src/shared/hooks/usePermissions.ts` | Yeni | Permission check hook |
| 29 | `src/shared/hooks/useModules.ts` | Yeni | Kurulu modülleri getiren hook |
| 30 | `src/shared/hooks/useOrganization.ts` | Yeni | Organizasyon branding hook |
| 31 | `src/shared/hooks/useCurrency.ts` | Yeni | Para birimi formatlama hook |
| 32 | `src/shared/hooks/useDirection.ts` | Yeni | RTL/LTR yön tespiti |
| 33 | `src/shared/components/layout/PortalLayout.tsx` | Yeni | Ana portal shell (sidebar + topbar + main + footer) |
| 34 | `src/shared/components/layout/Sidebar.tsx` | Yeni | Collapsible sidebar |
| 35 | `src/shared/components/layout/Topbar.tsx` | Yeni | Sticky topbar + dil seçici |
| 36 | `src/shared/components/layout/Footer.tsx` | Yeni | Footer |
| 37 | `src/shared/components/layout/SectionRenderer.tsx` | Yeni | Module-aware page builder infrastructure |
| 38 | `src/shared/components/guards/RequireAuth.tsx` | Yeni | Client-side auth guard |
| 39 | `src/shared/components/guards/RequirePermission.tsx` | Yeni | Permission guard |
| 40 | `src/shared/components/feedback/ErrorBoundary.tsx` | Yeni | Class-based error boundary |
| 41 | `src/shared/components/feedback/LoadingSkeleton.tsx` | Yeni | Skeleton loader |
| 42 | `src/shared/components/branding/BrandingProvider.tsx` | Yeni | CSS custom property tabanlı tenant branding |
| 43 | `src/shared/components/branding/TenantLogo.tsx` | Yeni | Logo / initials fallback |
| 44 | `src/locales/en/common.json` | Yeni | İngilizce common çeviriler |
| 45 | `src/locales/en/error.json` | Yeni | İngilizce hata mesajları |
| 46 | `src/locales/en/navigation.json` | Yeni | İngilizce navigasyon etiketleri |
| 47 | `src/locales/en/validation.json` | Yeni | İngilizce validasyon mesajları |
| 48 | `src/locales/tr/common.json` | Yeni | Türkçe common çeviriler |
| 49 | `src/locales/tr/error.json` | Yeni | Türkçe hata mesajları |
| 50 | `src/locales/tr/navigation.json` | Yeni | Türkçe navigasyon etiketleri |
| 51 | `src/locales/tr/validation.json` | Yeni | Türkçe validasyon mesajları |
| 52 | `src/shared/lib/stores/authStore.test.ts` | Yeni | authStore unit testleri (6 test) |
| 53 | `src/shared/lib/currency.test.ts` | Yeni | currency utility testleri (9 test) |
| 54 | `src/test/setup.ts` | Yeni | Vitest test setup |
| 55 | `vitest.config.ts` | Yeni | Vitest konfigürasyonu |
| 56 | `package.json` | Yeni | Tüm bağımlılıklar |
| 57 | `next.config.ts` | Yeni | Next.js konfigürasyonu |
| 58 | `tsconfig.json` | Yeni | TypeScript konfigürasyonu |
| 59 | `postcss.config.mjs` | Yeni | PostCSS (Tailwind CSS 4) |
| 60 | `eslint.config.mjs` | Yeni | ESLint flat config |
| 61 | `.gitignore` | Yeni | .gitignore |
| 62 | `package-lock.json` | Yeni | Lock file |
| 63 | `src/app/favicon.ico` | Yeni | Favicon |
| 64 | `docs/roadmap/ROADMAP.md` | Değişti | Portal Framework tamamlandı işareti |

---

## 1. Güvenlik — CRITICAL

### 1.1 [CRITICAL] `ErrorBoundary.tsx` — Hardcoded User-Facing Strings (Localization Standard İhlali)

**Dosya**: `src/shared/components/feedback/ErrorBoundary.tsx` satır 40, 44

**Sorun**: `ErrorBoundary` bileşeninin fallback UI'ında iki hardcoded İngilizce string mevcuttur:

```tsx
// ❌ CRITICAL VIOLATION — LOCALIZATION_STANDARDS.md
<p className="text-lg font-medium text-foreground">
  Something went wrong   {/* Hardcoded! */}
</p>
<button ...>
  Try again              {/* Hardcoded! */}
</button>
```

Bu, `LOCALIZATION_STANDARDS.md` Golden Rule kuralının — "NO hardcoded user-facing strings anywhere" — doğrudan ihlalidir. Sıfır tolerans kapsamındadır.

**Evet, class component hooks kullanamaz**, ancak çözüm mevcuttur:

**Seçenek A — Wrapper + prop injection (önerilen)**:
```tsx
// ErrorBoundaryFallback.tsx — bir fonksiyonel component
export function ErrorBoundaryFallback({ onReset }: { onReset: () => void }) {
  const t = useTranslations();
  return (
    <div className="flex min-h-[400px] flex-col items-center justify-center gap-4 p-8">
      <div className="text-4xl">⚠</div>
      <p className="text-lg font-medium text-foreground">
        {t('lockey_error_something_went_wrong')}
      </p>
      <button onClick={onReset} className="...">
        {t('lockey_common_try_again')}
      </button>
    </div>
  );
}

// ErrorBoundary'nin render'ında:
// this.props.fallback ?? <ErrorBoundaryFallback onReset={() => this.setState({ hasError: false })} />
```

**Seçenek B — `fallback` prop her zaman dışarıdan inject edilmeli** ve default fallback kaldırılmalıdır.

Ayrıca, `lockey_error_something_went_wrong` ve `lockey_common_try_again` anahtarları her iki dil dosyasına eklenmek zorundadır.

---

### 1.2 [CRITICAL] `next.config.ts` — Wildcard Image Hostname (SSRF/Güvenlik)

**Dosya**: `next.config.ts` satır 8-12

**Sorun**: `remotePatterns` konfigürasyonu tüm HTTPS kaynaklarına izin vermektedir:

```ts
// ❌ CRITICAL SECURITY ISSUE
remotePatterns: [
  {
    protocol: 'https',
    hostname: '**',  // Herhangi bir domain!
  },
],
```

Bu yapılandırma, Next.js image optimizasyon servisinin herhangi bir dış kaynaktan görüntü proxy'lemesine izin verir. Bu hem bir SSRF (Server-Side Request Forgery) riski hem de potansiyel bir bandwidth/cost saldırısı vektörüdür.

**Düzeltme**: Yalnızca güvenilen hostname'lere izin verin:
```ts
remotePatterns: [
  {
    protocol: 'https',
    hostname: '*.nexora.io',   // CDN/MinIO adresi
  },
  {
    protocol: 'https',
    hostname: process.env.MINIO_HOSTNAME ?? 'minio.internal',
  },
],
```

---

### 1.3 [CRITICAL] `auth.ts` — JWT Payload Manuel Decode Güvenlik Sorunu

**Dosya**: `src/shared/lib/auth.ts` satır 26-30, 41-47

**Sorun**: JWT payload manuel olarak base64 decode edilmekte ve imza doğrulaması yapılmadan gelen claim'ler kullanılmaktadır:

```ts
// ❌ PROBLEMATIC PATTERN
function decodeJwtPayload(token: string): Record<string, unknown> {
  const parts = token.split('.');
  if (parts.length !== 3) return {};
  const payload = Buffer.from(parts[1], 'base64').toString('utf-8');
  return JSON.parse(payload) as Record<string, unknown>;
}
```

Bu fonksiyon:
1. JWT imzasını doğrulamaz.
2. Kötü biçimlendirilmiş token'larda JSON.parse patlar (try/catch yok).
3. NextAuth'un `profile` callback'inde zaten doğrulanmış claim'ler mevcuttur — manuel decode gereksizdir.

NextAuth v5'in `jwt` callback'inde `account.access_token` zaten Keycloak tarafından imzalanmış ve provider tarafından doğrulanmıştır; ancak NextAuth'un kendi `profile` nesnesi varsa onu kullanmak daha güvenlidir. Ayrıca `account.id_token` üzerinden claim'ler alınabilir.

**Düzeltme**:
```ts
async jwt({ token, account, profile }) {
  if (account?.access_token) {
    token.accessToken = account.access_token;
    token.refreshToken = account.refresh_token;
    token.expiresAt = account.expires_at;

    // 'profile' Keycloak tarafından doğrulanmış ID token claim'leri içerir
    const keycloakProfile = profile as Record<string, unknown>;
    token.tenantId = keycloakProfile?.tenant_id as string | undefined;
    token.organizationId = keycloakProfile?.organization_id as string | undefined;
    token.permissions = (keycloakProfile?.permissions as string[]) ?? [];
  }
  // ...
}
```

Ayrıca token refresh bloğunda `JSON.parse` için try/catch eksiktir.

---

### 1.4 [CRITICAL] `middleware.ts` — Server-Side Auth Koruması Eksik

**Dosya**: `src/middleware.ts`

**Sorun**: Middleware yalnızca next-intl i18n routing yapmaktadır. NextAuth.js ile **server-side route koruması yoktur**:

```ts
// ❌ CURRENT — Sadece i18n, auth yok
export default createMiddleware(routing);
```

Tüm auth güvenliği `RequireAuth` client component üzerinden sağlanmaktadır. Bu şu riskleri doğurur:

1. **FOUC (Flash of Unauthenticated Content)**: Sayfa ilk render edildiğinde korumalı içerik kısa süreliğine görünebilir.
2. **SSR Bypass**: Server-side render edilen içerik `RequireAuth` çalışmadan önce client'a gönderilebilir.
3. **Search Engine Indexing Risk**: Bot'lar korumalı içeriği tarayabilir (SEO sorun değil ama içerik sızıntısı risk).
4. **FRONTEND_STANDARDS.md** → Security bölümü: "Redirect to login on 401 response" — bu da server-side olmalıdır.

**Düzeltme**: NextAuth middleware ile birleştirin:
```ts
// middleware.ts
import { auth } from '@/shared/lib/auth';
import createIntlMiddleware from 'next-intl/middleware';
import type { NextRequest } from 'next/server';
import { routing } from './i18n/routing';

const intlMiddleware = createIntlMiddleware(routing);

export default auth(async (req: NextRequest & { auth: unknown }) => {
  const isAuth = !!req.auth;
  const isAuthPage = req.nextUrl.pathname.includes('/auth/');
  const isPublicPath = isAuthPage;

  if (!isAuth && !isPublicPath) {
    const loginUrl = new URL('/auth/login', req.url);
    return Response.redirect(loginUrl);
  }

  return intlMiddleware(req);
});

export const config = {
  matcher: ['/((?!api|_next|.*\\..*).*)', '/'],
};
```

---

### 1.5 [MAJOR] `BrandingProvider.tsx` — CSS Injection Riski

**Dosya**: `src/shared/components/branding/BrandingProvider.tsx` satır 24-25

**Sorun**: `organization.logoUrl` değeri doğrulama yapılmadan CSS custom property olarak ayarlanmaktadır:

```ts
// ❌ POTENTIAL CSS INJECTION
root.style.setProperty('--brand-logo-url', `url(${organization.logoUrl})`);
```

Eğer `logoUrl` değeri `"data:text/css;base64,..."` veya `);}body{background:red;a{` gibi değerler içeriyorsa CSS injection gerçekleşebilir. Veri backend'den gelse de güven zinciri kırılabilir.

**Düzeltme**: URL formatını doğrulayın:
```ts
function isSafeUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

if (organization.logoUrl && isSafeUrl(organization.logoUrl)) {
  root.style.setProperty('--brand-logo-url', `url(${organization.logoUrl})`);
}
```

---

## 2. Localization Standards — CRITICAL

### 2.1 [CRITICAL] `not-found.tsx` içinde Locale-Aware Olmayan Redirect

**Dosya**: `src/app/[locale]/page.tsx`

**Sorun**: Root page bileşeni lokali hardcode ederek yönlendirme yapmaktadır:

```tsx
// ❌ CRITICAL — hardcoded locale
export default function HomePage() {
  redirect({ href: '/dashboard', locale: 'en' });
}
```

Bu, Türkçe URL'ye (`/tr`) gelen kullanıcıları İngilizce (`/en/dashboard`) dashboard'a yönlendirir. Multi-language desteği tamamen kırılmaktadır.

**Düzeltme**:
```tsx
// app/[locale]/page.tsx
import { redirect } from '@/i18n/navigation';

export default async function HomePage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  redirect({ href: '/dashboard', locale });
}
```

---

### 2.2 [CRITICAL] `LanguageSwitcher` — Default Value (Seçili Dil) Eksik

**Dosya**: `src/shared/components/layout/Topbar.tsx` satır 76-89

**Sorun**: `<select>` bileşeninin `value` ya da `defaultValue` prop'u yoktur; dolayısıyla mevcut aktif dil görsel olarak seçili görünmemektedir:

```tsx
// ❌ Aktif locale gösterilmiyor
<select
  onChange={(e) => {
    router.replace(pathname, { locale: e.target.value });
  }}
  className="..."
  aria-label={t('lockey_common_language')}
>
```

Türkçe arayüzdeyken dropdown hâlâ "English" gösterebilir veya belirsiz kalır.

**Düzeltme**:
```tsx
import { useLocale } from 'next-intl';

function LanguageSwitcher() {
  const currentLocale = useLocale();
  // ...
  return (
    <select
      value={currentLocale}
      onChange={(e) => router.replace(pathname, { locale: e.target.value })}
      // ...
    >
```

---

## 3. Mimari & Next.js Patterns — MAJOR

### 3.1 [MAJOR] `(portal)/layout.tsx` — Client Component Olması SSR Avantajını Ortadan Kaldırıyor

**Dosya**: `src/app/[locale]/(portal)/layout.tsx`

**Sorun**: Portal layout `'use client'` işaretlidir:

```tsx
'use client';  // ❌ Bu layout altındaki tüm sayfa ağacı client-side render olur

export default function PortalRouteLayout({ children }) {
  useAuth(); // side effect: /users/me fetch
  return (
    <RequireAuth>
      <PortalLayout>{children}</PortalLayout>
    </RequireAuth>
  );
}
```

Next.js 16 (App Router)'ın en önemli avantajı SSR/RSC (React Server Components) desteklidir. Portal layout'u client component yaparak:

- Dashboard, Profile ve tüm portal sayfaları SSR'dan mahrum kalır.
- Initial page load'da tüm JS bundle client'a gönderilir, sonra render başlar → First Contentful Paint gecikmesi.
- SEO kısıtlanır (portal public değilse sorun değil, ama performans etkilenir).

**Düzeltme**: Layout'u server component yapın; sadece etkileşimli parçaları (Sidebar, Topbar) client component tutun:

```tsx
// (portal)/layout.tsx — SERVER COMPONENT (no 'use client')
import { auth } from '@/shared/lib/auth';
import { redirect } from '@/i18n/navigation';
import { PortalLayout } from '@/shared/components/layout/PortalLayout';

export default async function PortalRouteLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const session = await auth();
  const { locale } = await params;

  if (!session) {
    redirect({ href: '/auth/login', locale });
  }

  return <PortalLayout>{children}</PortalLayout>;
}
```

`useAuth()` çağrısı bir server action veya ayrı bir client component `<AuthInitializer />` içine alınmalıdır.

---

### 3.2 [MAJOR] `auth.ts` — Token Refresh Başarısızlığında Oturum Eksik Invalidation

**Dosya**: `src/shared/lib/auth.ts` satır 63-66

**Sorun**: Refresh token başarısız olduğunda yalnızca `accessToken` undefined yapılmakta, NextAuth best practice olan error flag set edilmemektedir:

```ts
// ❌ Yetersiz hata yönetimi
} catch {
  token.accessToken = undefined;
}
```

Bu durumda:
- Oturum geçersiz bir token ile devam eder.
- Sonraki API istekleri `Authorization: Bearer undefined` gönderir.
- Kullanıcı login ekranına yönlendirilmez; "askıda" bir oturum oluşur.

**Düzeltme (NextAuth best practice)**:
```ts
} catch {
  return { ...token, error: 'RefreshAccessTokenError' as const };
}

// session callback'inde:
async session({ session, token }) {
  if (token.error === 'RefreshAccessTokenError') {
    // Session'ı geçersiz kıl — middleware veya client bunu yakalar
    return { ...session, error: 'RefreshAccessTokenError' };
  }
  // ...
}
```

Client tarafında `session.error === 'RefreshAccessTokenError'` kontrolü ile login'e redirect yapılmalıdır.

---

### 3.3 [MAJOR] `useAuth.ts` — Potansiyel Sonsuz Döngü (Race Condition)

**Dosya**: `src/shared/hooks/useAuth.ts` satır 19-41

**Sorun**: `/identity/users/me` endpoint'i başarısız olduğunda `clearSession()` çağrılmaktadır. Ancak bu `user`'ı `null` yapar; bir sonraki render'da `!user` koşulu yeniden `true` olur ve tekrar fetch tetiklenir:

```ts
// ❌ Sonsuz döngü riski
useEffect(() => {
  if (status === 'authenticated' && session?.accessToken) {
    setAuthToken(session.accessToken);
    if (!user) {  // <- clearSession sonrası null, tekrar fetch tetiklenir
      api.get<UserInfo>('/identity/users/me')
        .then(...)
        .catch(() => {
          clearSession();  // <- user null yapılır → tekrar buraya
        });
    }
  }
  // ...
}, [session, status, user, ...]);
```

Ağ hatası veya 403 durumunda bu döngü sürekli API isteği atmaya devam eder.

**Düzeltme**: Hata durumunu ayrı bir state ile takip edin:
```ts
const [fetchFailed, setFetchFailed] = useState(false);

useEffect(() => {
  if (status === 'authenticated' && session?.accessToken && !user && !fetchFailed) {
    setAuthToken(session.accessToken);
    api.get<UserInfo>('/identity/users/me')
      .then((userInfo) => setSession({ ... }))
      .catch(() => {
        setFetchFailed(true);
        clearSession();
      });
  }
  if (status !== 'authenticated') {
    setFetchFailed(false); // session değiştiğinde reset
  }
}, [session, status, user, fetchFailed, ...]);
```

---

### 3.4 [MINOR] `i18n/request.ts` — Mesaj Namespace'leri Merge Edilmiş (Ölçekleme Sorunu)

**Dosya**: `src/i18n/request.ts` satır 9-21

**Sorun**: Tüm namespace'ler (common, error, validation, navigation) tek bir düz nesneye merge edilerek yüklenmektedir:

```ts
return {
  locale,
  messages: {
    ...common.default,     // lockey_common_*
    ...error.default,      // lockey_error_*
    ...validation.default, // lockey_validation_*
    ...navigation.default, // lockey_nav_*
  },
};
```

Phase 1.5'te 4 namespace ve ~80 key için sorunsuz çalışır. Ancak Phase 2+'de modül namespace'leri (donations.json, crm.json, sponsorship.json, vs.) eklendiğinde:

1. Her sayfa tüm modüllerin çevirilerini yükler (gereksiz payload).
2. Flat merge'de key çakışması riski oluşur.
3. next-intl'nin namespace isolation özelliğinden yararlanılamaz.

Bu Phase 1.5 için **Major** değil; ancak **Phase 2 başlamadan önce** refactor edilmelidir. next-intl'nin namespace destekli yapısına geçiş planlanmalıdır:
```ts
return {
  locale,
  messages: { common, error, validation, navigation }, // Namespace korunur
};

// Kullanım: t('common.lockey_common_save')
// veya: const t = useTranslations('common');
```

---

## 4. Layout & UI Hataları — MAJOR

### 4.1 [MAJOR] `PortalLayout.tsx` — İçerik Sticky Topbar Altına Kayıyor

**Dosya**: `src/shared/components/layout/PortalLayout.tsx` satır 27-32

**Sorun**: `<main>` elementi `pt-16` (ya da eşdeğeri) sınıfı taşımamaktadır. `Topbar` `sticky top-0 z-30 h-16` ile tanımlıdır; dolayısıyla `<main>` içeriği topbar'ın **altına gizlenerek** başlar.

```tsx
// ❌ pt-16 eksik — içerik topbar arkasında başlar
<main
  className={cn(
    'min-h-[calc(100vh-8rem)] p-6 transition-all duration-300',
    sidebarOpen ? 'ml-64' : 'ml-16',
    // pt-16 OLMALI
  )}
>
```

**Düzeltme**:
```tsx
<main
  className={cn(
    'min-h-[calc(100vh-8rem)] p-6 pt-16 transition-all duration-300',
    sidebarOpen ? 'ml-64' : 'ml-16',
  )}
>
```

---

### 4.2 [MAJOR] `Sidebar.tsx` — Navigation Filter Mantık Hatası

**Dosya**: `src/shared/components/layout/Sidebar.tsx` satır 48-54

**Sorun**: `filter` callback'i item parametresini kullanmamaktadır:

```ts
// ❌ Arrow function item'ı ignore ediyor
.flatMap((m) =>
  m.navigation.filter(() =>          // Parametre yok!
    m.permissions.some((p) => hasPermission(p)),
  ),
)
```

Bu kod işlevsel olarak çalışır (her nav item için aynı boolean döner — modül seviyesinde tümü göster/gizle), fakat:

1. Okunabilirlik açısından yanıltıcıdır; okuyucu per-item filtreleme bekler.
2. Gelecekte `PortalNavigationItem`'a `permissions` alanı eklendiğinde bu kod yanlış çalışır.
3. Tip güvenliği açısından kullanılmayan parametre TypeScript lint uyarısı doğurabilir.

**Düzeltme** (niyet açık olmalı):
```ts
// Modül seviyesinde tümü göster veya gizle (mevcut niyet)
.flatMap((m) => {
  const canAccessModule = m.permissions.some((p) => hasPermission(p));
  return canAccessModule ? m.navigation : [];
})
```

---

## 5. State Management & Hooks

### 5.1 [MAJOR] `useOrganization.ts` — `staleTime` Eksik

**Dosya**: `src/shared/hooks/useOrganization.ts` satır 22-29

**Sorun**: Organizasyon branding verisi `staleTime` belirtilmeden sorgulanmaktadır:

```ts
const query = useQuery({
  queryKey: organizationKeys.detail(organizationId ?? ''),
  queryFn: () => api.get<OrganizationBranding>(...),
  enabled: !!organizationId,
  // staleTime YOK
});
```

Bu, `API_INTEGRATION_GUIDE.md` tavsiyesine aykırıdır: "staleTime: 5 minutes for details". `BrandingProvider` her render'da `useOrganization` çağırır; `staleTime` eksikliği window focus her değiştiğinde gereksiz refetch'e neden olur.

**Düzeltme**:
```ts
const query = useQuery({
  queryKey: organizationKeys.detail(organizationId ?? ''),
  queryFn: () => api.get<OrganizationBranding>(...),
  enabled: !!organizationId,
  staleTime: 5 * 60 * 1000, // 5 dakika — branding nadiren değişir
});
```

---

### 5.2 [MINOR] `useModules.ts` — `hasModule` Memoize Edilmemiş

**Dosya**: `src/shared/hooks/useModules.ts` satır 44

**Sorun**: `hasModule` her render'da yeni bir fonksiyon referansı oluşturur:

```ts
const hasModule = (moduleName: string): boolean =>
  installedModuleNames.has(moduleName);
```

`useCallback` yoksa her render'da referans yenilenir; `hasModule` bir child component'e prop olarak geçirilirse gereksiz re-render tetiklenebilir.

**Düzeltme**:
```ts
const hasModule = useCallback(
  (moduleName: string) => installedModuleNames.has(moduleName),
  [installedModuleNames],
);
```

---

### 5.3 [MINOR] `useAuth.ts` — `useEffect` Error Handling Sessiz Kalıyor

**Dosya**: `src/shared/hooks/useAuth.ts` satır 34-36

**Sorun**: `/users/me` çağrısı başarısız olduğunda hata yutulmaktadır:

```ts
.catch(() => {
  // User fetch failed — clear session
  clearSession();
});
```

`OBSERVABILITY_STANDARDS.md` ve `FRONTEND_STANDARDS.md` gereği API hataları yönetilmeli ve kullanıcıya bildirilmelidir. Silent fail burada kullanıcıyı boş bir ekranda bırakabilir.

**Düzeltme**: Hata durumunda `lockey_error_session_expired` toast gösterin veya login sayfasına yönlendirin.

---

### 5.4 [INFO] `uiStore.ts` — Dark Mode State Eksik

**Dosya**: `src/shared/lib/stores/uiStore.ts`

`globals.css`'te CSS media query ile dark mode desteği mevcut, ancak `uiStore`'da `theme` state'i yoktur. Kullanıcı kontrolü (dark/light toggle) ilerleyen aşamalarda buraya eklenmelidir. Şu an sadece sistem tercihi geçerli.

---

## 6. Test Coverage — MAJOR

### 6.1 [MAJOR] Commit Mesajındaki "15 Test" İddiası ile Gerçek Test Sayısı Uyuşmuyor

**Sorun**: Commit mesajı "15 frontend tests" iddiasında bulunmaktadır. Gerçek test sayısı:
- `currency.test.ts`: 9 test case
- `authStore.test.ts`: 6 test case
- **Toplam: 15** ✓

Bu sayı doğrudur. Ancak test **kapsamı** yetersizdir (aşağıya bakın).

---

### 6.2 [MAJOR] Kritik Bileşen ve Hook'lar Testi Eksik

**Sorun**: `FRONTEND_STANDARDS.md` Section 11.4: "Minimum: All shared components + hooks" gereksinimi karşılanmamaktadır.

Test edilmemiş kritik shared dosyalar:

| Dosya | Öncelik |
|-------|---------|
| `RequireAuth.tsx` | Yüksek — auth guard, redirect mantığı |
| `RequirePermission.tsx` | Yüksek — permission kontrol mantığı |
| `SectionRenderer.tsx` | Yüksek — modül section kompozisyonu |
| `useAuth.ts` | Yüksek — session sync, /users/me fetch |
| `useModules.ts` | Yüksek — modül filtreleme mantığı |
| `usePermissions.ts` | Orta |
| `api.ts` | Yüksek — `extractApiError` mantığı |
| `useDirection.ts` | Düşük |
| `useCurrency.ts` | Orta |

En az şu testler Phase 1.5 tamamlanmadan eklenmelidir:
- `RequireAuth` → loading, unauthenticated redirect, authenticated render
- `RequirePermission` → all/any mode, fallback render
- `SectionRenderer` → section filtering, sorting, permission checks
- `useModules` → installed filter, hasModule

---

### 6.3 [MINOR] `currency.test.ts` — Negatif Amount Format Assertion Gevşek

**Dosya**: `src/shared/lib/currency.test.ts` satır 27-29

**Sorun**: Negatif tutar testi yalnızca `toContain('50.00')` ile doğrulamaktadır:

```ts
it('should handle negative amounts', () => {
  const result = formatMoney({ amount: -50, currency: 'USD' });
  expect(result).toContain('50.00');  // ❌ '-$50.00' veya '($50.00)' ayrımı yok
});
```

`Intl.NumberFormat` locale'e göre negatif parantez (`($50.00)`) ya da eksi işareti (`-$50.00`) kullanabilir. Test hangi formatın beklediğini açıkça belirtmelidir.

**Düzeltme**:
```ts
it('should handle negative amounts', () => {
  const result = formatMoney({ amount: -50, currency: 'USD' });
  expect(result).toBe('-$50.00');
});
```

---

## 7. Konfigürasyon

### 7.1 [MAJOR] `next.config.ts` — `next-intl` Plugin ile TypeScript İmport

**Dosya**: `next.config.ts`

`next.config.ts` uzantısı TypeScript'tir ancak `createNextIntlPlugin` çağrısında `withNextIntl(nextConfig)` olarak export edilmektedir. Bu yapı genellikle sorunsuz çalışır, ancak Next.js 16 için `.ts` config dosyasının bazı edge case'lerinde type inference problemleri doğurduğu bildirilmiştir.

Ek olarak, `images.remotePatterns` sorunu ayrı bir CRITICAL olarak (#1.2) işlendi.

---

### 7.2 [MINOR] `vitest.config.ts` — `@vitejs/plugin-react` `devDependencies`'de Eksik

**Dosya**: `package.json`

```json
"devDependencies": {
  "@vitejs/plugin-react": "^4.7.0",  // ✓ Mevcut
  "@vitest/coverage-v8": ...         // ❌ EKSIK — coverage için gerekli
}
```

`vitest.config.ts`'de coverage yapılandırması (`reporter: ['text', 'json', 'html']`) mevcuttur, ancak `package.json`'da `@vitest/coverage-v8` ya da `@vitest/coverage-istanbul` bağımlılığı yoktur. `vitest run --coverage` komutu bu paket olmadan hata verir.

**Düzeltme**: `devDependencies`'e ekleyin:
```json
"@vitest/coverage-v8": "^3.2.4"
```

---

## 8. Kod Kalitesi

### 8.1 [MINOR] `TenantLogo.tsx` — Fallback "N" Hardcoded

**Dosya**: `src/shared/components/branding/TenantLogo.tsx` satır 30-39

**Sorun**: Organization yüklenene kadar `"N"` hardcoded placeholder gösterilmektedir:

```tsx
<div ...>
  N  {/* Hardcoded — kullanıcı arayüzünde gösterilen karakter */}
</div>
```

Localization standardı açısından bu bir harf olduğu için çeviriye ihtiyaç duyulmasa da, organizasyon yüklenirken bir skeleton göstermek daha iyi bir UX sağlar:

```tsx
if (!organization) {
  return <div className={cn('animate-pulse rounded-md bg-muted', className)}
    style={{ width: size, height: size }} />;
}
```

---

### 8.2 [MINOR] `Topbar.tsx` — `aria-label` Button için Missing Locale Key

**Dosya**: `src/shared/components/layout/Topbar.tsx` satır 42

**Sorun**: Sidebar toggle button için `aria-label="Toggle sidebar"` hardcoded İngilizce string kullanılmaktadır:

```tsx
// ❌ Hardcoded aria-label
<button
  onClick={toggleSidebar}
  aria-label="Toggle sidebar"  // Localization ihlali
>
```

Accessibility açısından screen reader'lar bu metni okur — dolayısıyla çevrilmesi gerekmektedir.

**Düzeltme**:
```tsx
aria-label={t('lockey_common_toggle_sidebar')}
```

Ve `lockey_common_toggle_sidebar` her iki dil dosyasına eklenmelidir.

---

### 8.3 [INFO] `_registry.ts` — Boş Registry Belgelenmeli

**Dosya**: `src/modules/_registry.ts`

`allPortalModules` şu an boş bir array; bu Phase 1.5 için tasarım gereği doğrudur. Ancak modüller nasıl ekleneceğine dair inline bir comment veya link eklemek faydalı olur. Mevcut comment zaten bu bilgiyi içermektedir — bu nedenle sadece bilgilendirme amaçlı belirtilmiştir.

---

## 9. Olumlu Bulgular

Bu bölüm, commit'te başarılı şekilde uygulanan pattern ve kararları belgelemektedir.

### ✅ TypeScript Strict Mode

`tsconfig.json`'da `"strict": true` aktifdir. Tüm dosyalar `any` kullanmadan strict mode'a uymaktadır.

### ✅ next-intl Entegrasyonu

next-intl `FRONTEND_STANDARDS.md` ve `LOCALIZATION_STANDARDS.md` gereksinimlerine uygun kurulmuştur:
- `routing.ts` → locale yapısı
- `request.ts` → server-side message loading
- `navigation.ts` → locale-aware Link/Router
- Tüm bileşenler `useTranslations()` kullanmaktadır (hardcoded string ihlalleri hariç)
- `lockey_` prefix'i tutarlı şekilde uygulanmıştır

### ✅ Zustand Store Tasarımı

`authStore.ts` ve `uiStore.ts`, `FRONTEND_STANDARDS.md` Section 6.2'deki pattern'a uygundur:
- Minimal store (auth + ui only)
- `hasPermission` ve `hasAnyPermission` yardımcıları doğru implemente edilmiştir
- `useAuthStore.getState()` non-reactive erişim için kullanılabilir

### ✅ TanStack Query Konfigürasyonu

`query.ts` factory pattern ile SSR-safe şekilde implemente edilmiştir (`useState(() => createQueryClient())`). `API_INTEGRATION_GUIDE.md` ile uyumlu varsayılanlar (staleTime 30s, retry 1, refetchOnWindowFocus false) ayarlıdır.

### ✅ ApiEnvelope Uyumu

`shared/types/api.ts` backend API kontratı ile birebir eşleşmektedir. `api.ts`'deki `extractApiError` `lockey_` key döndürmektedir.

### ✅ Module Manifest Sistemi

`PortalModuleManifest`, `PortalSection`, `SectionPosition` tipleri `MODULE_SYSTEM.md` Section 7 (Portal) ile uyumludur. `SectionRenderer` page builder altyapısı doğru implemente edilmiştir.

### ✅ RTL Desteği Altyapısı

`useDirection.ts` ve `[locale]/layout.tsx`'te `dir` hesaplaması mevcuttur. Tailwind RTL utilities kullanılmaktadır. Arapça ve diğer RTL diller için altyapı hazırdır.

### ✅ NextAuth.js v5 + Keycloak

Token refresh mekanizması implemente edilmiştir. `httpOnly` cookie tabanlı session (`strategy: 'jwt'`) `FRONTEND_STANDARDS.md` güvenlik gereksinimlerini karşılamaktadır.

### ✅ Çeviri Dosyası Eşleşmesi

`en/` ve `tr/` her iki locale dosyasında tüm `lockey_` keyler birebir eşleşmektedir. LOCALIZATION_STANDARDS.md Section 4.6: "New keys must be added to ALL language files simultaneously" kuralı karşılanmıştır.

### ✅ `RequirePermission` — `mode` Parametresi

`mode: 'all' | 'any'` seçeneği Frontend Standards'ın her iki `hasPermission`/`hasAnyPermission` pattern'ını karşılamaktadır.

### ✅ Test Altyapısı

Vitest + jsdom + React Testing Library konfigürasyonu `FRONTEND_STANDARDS.md` Section 11 ile tam uyumludur.

---

## 10. Düzeltme Özeti ve Öncelik Sırası

| Öncelik | # | Dosya | Sorun | Severity |
|---------|---|-------|-------|----------|
| 1 | 1.4 | `middleware.ts` | Server-side auth koruması eksik | CRITICAL |
| 2 | 1.1 | `ErrorBoundary.tsx` | Hardcoded user-facing strings | CRITICAL |
| 3 | 2.1 | `page.tsx` | Hardcoded 'en' locale redirect | CRITICAL |
| 4 | 1.2 | `next.config.ts` | Wildcard image hostname | CRITICAL |
| 5 | 1.3 | `auth.ts` | JWT manuel decode + refresh error | CRITICAL |
| 6 | 2.2 | `Topbar.tsx` | LanguageSwitcher selected state eksik | CRITICAL |
| 7 | 3.1 | `(portal)/layout.tsx` | Client layout → SSR kaybı | MAJOR |
| 8 | 4.1 | `PortalLayout.tsx` | pt-16 eksik → layout bug | MAJOR |
| 9 | 4.2 | `Sidebar.tsx` | Filter mantık hatası | MAJOR |
| 10 | 3.2 | `auth.ts` | Refresh fail → eksik invalidation | MAJOR |
| 11 | 3.3 | `useAuth.ts` | Sonsuz döngü riski | MAJOR |
| 12 | 5.1 | `useOrganization.ts` | staleTime eksik | MAJOR |
| 13 | 6.2 | Tests | Kritik component testleri eksik | MAJOR |
| 14 | 7.2 | `package.json` | @vitest/coverage-v8 eksik | MAJOR |
| 15 | 1.5 | `BrandingProvider.tsx` | CSS injection riski | MAJOR |
| 16 | 5.2 | `useModules.ts` | hasModule memoize edilmemiş | MINOR |
| 17 | 5.3 | `useAuth.ts` | Silent error catch | MINOR |
| 18 | 6.3 | `currency.test.ts` | Negative amount assertion | MINOR |
| 19 | 8.1 | `TenantLogo.tsx` | Hardcoded 'N' placeholder | MINOR |
| 20 | 8.2 | `Topbar.tsx` | Hardcoded aria-label | MINOR |
| 21 | 7.1 | `next.config.ts` | TS config edge case | MINOR |
| 22 | 3.4 | `i18n/request.ts` | Flat merge ölçekleme sorunu | MINOR |

---

## Final Verdict

```
STATUS: CHANGES_REQUESTED

Kritik Bloklar (merge edilmeden önce çözülmeli):
- [CRITICAL-1] middleware.ts → NextAuth server-side route protection eklenmeli
- [CRITICAL-2] ErrorBoundary.tsx → Hardcoded string (lockey_ ile değiştirilmeli)
- [CRITICAL-3] page.tsx → Hardcoded 'en' locale kaldırılmalı
- [CRITICAL-4] next.config.ts → Wildcard hostname daraltılmalı
- [CRITICAL-5] auth.ts → JWT decode + refresh error handling düzeltilmeli
- [CRITICAL-6] Topbar.tsx → LanguageSwitcher value prop eklenmeli

Genel Değerlendirme:
Commit, Phase 1.5 için solid bir altyapı kurmuştur. next-intl entegrasyonu,
Zustand store tasarımı, TanStack Query konfigürasyonu ve modül manifest sistemi
standartlara uygun şekilde implemente edilmiştir. Test coverage altyapısı
doğrudur ancak kritik bileşenler için test eksiktir.

Yukarıdaki 6 kritik ve 10 major bulgu giderildikten sonra merge için uygundur.
```
