'use client';

import {
  FileText,
  GraduationCap,
  Heart,
  Home,
  type LucideIcon,
  Calendar,
  ClipboardList,
  HandCoins,
} from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo } from 'react';

import { Link, usePathname } from '@/i18n/navigation';
import { cn } from '@/shared/lib/utils';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useModules } from '@/shared/hooks/useModules';
import { usePermissions } from '@/shared/hooks/usePermissions';
import type { PortalNavigationItem } from '@/shared/types/module';

const iconMap: Record<string, LucideIcon> = {
  Home,
  Heart,
  HandCoins,
  Calendar,
  FileText,
  GraduationCap,
  ClipboardList,
};

function getIcon(iconName: string): LucideIcon {
  return iconMap[iconName] ?? Home;
}

export function Sidebar() {
  const tc = useTranslations('common');
  const tn = useTranslations('navigation');
  const pathname = usePathname();
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);
  const { activeModules } = useModules();
  const { hasPermission } = usePermissions();

  const allNavItems = useMemo<PortalNavigationItem[]>(
    () => [
      { label: 'lockey_nav_dashboard', path: '/dashboard', icon: 'Home' },
      ...activeModules.flatMap((m) => {
        const canAccessModule = m.permissions.some((p) => hasPermission(p));
        return canAccessModule ? m.navigation : [];
      }),
    ],
    [activeModules, hasPermission],
  );

  return (
    <aside
      className={cn(
        'fixed start-0 top-0 z-40 h-screen border-e border-border bg-background transition-all duration-300',
        sidebarOpen ? 'w-[var(--sidebar-width-open)]' : 'w-[var(--sidebar-width-closed)]',
      )}
    >
      <div className="flex h-16 items-center justify-center border-b border-border px-4">
        {sidebarOpen && (
          <span className="text-lg font-semibold text-foreground">
            {tc('lockey_common_app_name')}
          </span>
        )}
      </div>

      <nav className="flex flex-col gap-1 p-2">
        {allNavItems.map((item) => {
          const Icon = getIcon(item.icon);
          const isActive = pathname === item.path || pathname.startsWith(item.path + '/');

          return (
            <Link
              key={item.path}
              href={item.path}
              className={cn(
                'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                isActive
                  ? 'bg-accent text-accent-foreground'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground',
              )}
            >
              <Icon className="h-5 w-5 shrink-0" />
              {sidebarOpen && <span>{tn(item.label)}</span>}
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
