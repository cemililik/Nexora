import { useTranslation } from 'react-i18next';
import { LogOut, Menu, Moon, Sun, Monitor, Languages } from 'lucide-react';

import { toast } from 'sonner';

import { Button } from '@/shared/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/components/ui/dropdown-menu';
import { Avatar, AvatarFallback } from '@/shared/components/ui/avatar';
import { cn } from '@/shared/lib/utils';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { getKeycloak } from '@/shared/lib/auth';

/** Admin top bar with user menu, language switcher, and theme toggle. */
export function Topbar() {
  const { t, i18n } = useTranslation('common');
  const user = useAuthStore((s) => s.user);
  const toggleSidebar = useUiStore((s) => s.toggleSidebar);
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);
  const setTheme = useUiStore((s) => s.setTheme);
  const theme = useUiStore((s) => s.theme);

  const initials = user
    ? `${user.firstName?.charAt(0) ?? ''}${user.lastName?.charAt(0) ?? ''}` || '?'
    : '?';

  const handleLogout = () => {
    const keycloak = getKeycloak();
    if (keycloak) {
      void keycloak.logout({ redirectUri: window.location.origin + '/login' });
    } else {
      toast.error(t('lockey_error_logout_failed', { ns: 'error' }));
      window.location.href = '/login';
    }
  };

  const handleLanguageChange = (lang: string) => {
    void i18n.changeLanguage(lang);
  };

  return (
    <header
      className={cn(
        'fixed inset-x-0 top-0 z-20 flex h-16 items-center justify-between border-b bg-card px-4 transition-all duration-300',
        sidebarOpen
          ? 'ms-[var(--sidebar-width-open)]'
          : 'ms-[var(--sidebar-width-closed)]',
      )}
    >
      <Button
        type="button"
        variant="ghost"
        size="icon"
        onClick={toggleSidebar}
        aria-label={t('lockey_common_toggle_sidebar')}
      >
        <Menu className="h-5 w-5" />
      </Button>

      <div className="flex items-center gap-2">
        {/* Language Switcher */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button type="button" variant="ghost" size="icon" aria-label={t('lockey_common_language')}>
              <Languages className="h-5 w-5" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>{t('lockey_common_language')}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => handleLanguageChange('en')}>
              {t('lockey_common_locale_en')}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => handleLanguageChange('tr')}>
              {t('lockey_common_locale_tr')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        {/* Theme Toggle */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button type="button" variant="ghost" size="icon" aria-label={t('lockey_common_theme')}>
              {theme === 'dark' ? (
                <Moon className="h-5 w-5" />
              ) : theme === 'light' ? (
                <Sun className="h-5 w-5" />
              ) : (
                <Monitor className="h-5 w-5" />
              )}
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>{t('lockey_common_theme')}</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => setTheme('light')}>
              <Sun className="me-2 h-4 w-4" />
              {t('lockey_common_theme_light')}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => setTheme('dark')}>
              <Moon className="me-2 h-4 w-4" />
              {t('lockey_common_theme_dark')}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => setTheme('system')}>
              <Monitor className="me-2 h-4 w-4" />
              {t('lockey_common_theme_system')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        {/* User Menu */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button type="button" variant="ghost" size="icon" className="rounded-full">
              <Avatar className="h-8 w-8">
                <AvatarFallback className="text-xs">{initials}</AvatarFallback>
              </Avatar>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {user && (
              <>
                <DropdownMenuLabel>
                  {user.firstName} {user.lastName}
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
              </>
            )}
            <DropdownMenuItem onClick={handleLogout}>
              <LogOut className="me-2 h-4 w-4" />
              {t('lockey_common_logout')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
