import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NavLink, useLocation } from 'react-router';
import {
  Building2,
  ChevronDown,
  LayoutDashboard,
  type LucideIcon,
  Server,
  Shield,
  ShieldCheck,
  Users,
  FileText,
  Bell,
  Contact,
  Building,
  ScrollText,
  Blocks,
  Tag,
  FolderOpen,
  Stamp,
  Send,
  Clock,
} from 'lucide-react';

import { cn } from '@/shared/lib/utils';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useModules } from '@/shared/hooks/useModules';
import type { AdminNavigationItem } from '@/shared/types/module';

const iconMap: Record<string, LucideIcon> = {
  Building2,
  LayoutDashboard,
  Server,
  Shield,
  ShieldCheck,
  Users,
  FileText,
  Bell,
  Contact,
  Building,
  ScrollText,
  Blocks,
  Tag,
  FolderOpen,
  Stamp,
  Send,
  Clock,
};

/** Core navigation items always shown (not module-dependent). */
const coreNavigation: AdminNavigationItem[] = [
  {
    label: 'lockey_nav_dashboard',
    path: '/dashboard',
    icon: 'LayoutDashboard',
  },
];

function NavItem({
  item,
  collapsed,
  namespaces,
}: {
  item: AdminNavigationItem;
  collapsed: boolean;
  namespaces: string[];
}) {
  const { t } = useTranslation(namespaces);
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const Icon = iconMap[item.icon];
  const isActive = location.pathname.startsWith(item.path);

  if (item.children?.length) {
    return (
      <div>
        <button
          type="button"
          onClick={() => setOpen(!open)}
          className={cn(
            'flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors',
            'hover:bg-accent hover:text-accent-foreground',
            isActive && 'bg-accent text-accent-foreground',
            collapsed && 'justify-center px-2',
          )}
        >
          {Icon && <Icon className="h-4 w-4 shrink-0" />}
          {!collapsed && (
            <>
              <span className="flex-1 text-start">{t(item.label)}</span>
              <ChevronDown
                className={cn(
                  'h-4 w-4 transition-transform',
                  open && 'rotate-180',
                )}
              />
            </>
          )}
        </button>
        {!collapsed && open && (
          <div className="ms-4 mt-1 space-y-1 border-s ps-3">
            {item.children.map((child) => (
              <NavItem key={child.path} item={child} collapsed={false} namespaces={namespaces} />
            ))}
          </div>
        )}
      </div>
    );
  }

  return (
    <NavLink
      to={item.path}
      className={({ isActive: active }) =>
        cn(
          'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors',
          'hover:bg-accent hover:text-accent-foreground',
          active && 'bg-accent font-medium text-accent-foreground',
          collapsed && 'justify-center px-2',
        )
      }
    >
      {Icon && <Icon className="h-4 w-4 shrink-0" />}
      {!collapsed && <span>{t(item.label)}</span>}
    </NavLink>
  );
}

/** Admin sidebar with module-aware navigation and collapse support. */
export function Sidebar() {
  const { t } = useTranslation('common');
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);
  const { activeModules } = useModules();

  // Build namespace list dynamically from active modules
  const namespaces = ['navigation', ...activeModules.map((m) => m.name), 'common'];

  return (
    <aside
      className={cn(
        'fixed inset-y-0 start-0 z-30 flex flex-col border-e bg-card transition-all duration-300',
        sidebarOpen
          ? 'w-[var(--sidebar-width-open)]'
          : 'w-[var(--sidebar-width-closed)]',
      )}
    >
      <div className="flex h-16 items-center justify-center border-b px-4">
        {sidebarOpen ? (
          <span className="text-lg font-semibold">{t('lockey_common_brand_name')}</span>
        ) : (
          <span className="text-lg font-semibold">{t('lockey_common_brand_short')}</span>
        )}
      </div>

      <nav className="flex-1 space-y-1 overflow-y-auto p-2">
        {coreNavigation.map((item) => (
          <NavItem key={item.path} item={item} collapsed={!sidebarOpen} namespaces={namespaces} />
        ))}

        {activeModules.map((module) =>
          module.navigation.map((item) => (
            <NavItem key={item.path} item={item} collapsed={!sidebarOpen} namespaces={namespaces} />
          )),
        )}
      </nav>
    </aside>
  );
}
