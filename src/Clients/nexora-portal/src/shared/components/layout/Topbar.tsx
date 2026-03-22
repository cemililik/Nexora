'use client';

import { LogOut, Menu, User } from 'lucide-react';
import { signOut } from 'next-auth/react';
import { useLocale, useTranslations } from 'next-intl';

import { Link, usePathname, useRouter } from '@/i18n/navigation';
import { cn } from '@/shared/lib/utils';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { TenantLogo } from '@/shared/components/branding/TenantLogo';
import { routing } from '@/i18n/routing';

export function Topbar() {
  const t = useTranslations();
  const currentLocale = useLocale();
  const user = useAuthStore((s) => s.user);
  const toggleSidebar = useUiStore((s) => s.toggleSidebar);
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);

  return (
    <header
      className={cn(
        'sticky top-0 z-30 flex h-16 items-center justify-between border-b border-border bg-background px-4 transition-all duration-300',
        sidebarOpen ? 'ms-[var(--sidebar-width-open)]' : 'ms-[var(--sidebar-width-closed)]',
      )}
    >
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={toggleSidebar}
          className="rounded-md p-2 text-muted-foreground hover:bg-muted hover:text-foreground"
          aria-label={t('lockey_common_toggle_sidebar')}
        >
          <Menu className="h-5 w-5" />
        </button>
        <TenantLogo size={28} />
      </div>

      <div className="flex items-center gap-3">
        <LanguageSwitcher />

        {user && (
          <div className="flex items-center gap-2">
            <Link
              href="/profile"
              className="flex items-center gap-2 rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              <User className="h-4 w-4" />
              <span className="hidden sm:inline">
                {user.firstName} {user.lastName}
              </span>
            </Link>

            <button
              type="button"
              onClick={() => signOut({ callbackUrl: `/${currentLocale}/auth/login` })}
              className="rounded-md p-2 text-muted-foreground hover:bg-muted hover:text-foreground"
              aria-label={t('lockey_common_logout')}
            >
              <LogOut className="h-4 w-4" />
            </button>
          </div>
        )}
      </div>
    </header>
  );
}

function LanguageSwitcher() {
  const t = useTranslations();
  const currentLocale = useLocale();
  const pathname = usePathname();
  const router = useRouter();

  return (
    <div className="relative">
      <select
        value={currentLocale}
        onChange={(e) => {
          router.replace(pathname, { locale: e.target.value });
        }}
        className="appearance-none rounded-md border border-border bg-background px-3 py-1.5 text-sm text-foreground cursor-pointer"
        aria-label={t('lockey_common_language')}
      >
        {routing.locales.map((locale) => (
          <option key={locale} value={locale}>
            {t(`lockey_common_locale_${locale}`)}
          </option>
        ))}
      </select>
    </div>
  );
}
