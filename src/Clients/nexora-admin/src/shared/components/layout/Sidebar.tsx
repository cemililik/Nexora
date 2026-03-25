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
  Copy,
  PenTool,
  FileBarChart,
  FileCode,
} from 'lucide-react';

import { cn } from '@/shared/lib/utils';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useModules } from '@/shared/hooks/useModules';
import type { AdminModuleManifest, AdminNavigationItem } from '@/shared/types/module';

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
  Copy,
  PenTool,
  FileBarChart,
  FileCode,
};

/** Module display name keys for sidebar group headers. */
const moduleDisplayNames: Record<string, string> = {
  identity: 'lockey_common_module_identity',
  contacts: 'lockey_common_module_contacts',
  documents: 'lockey_common_module_documents',
  notifications: 'lockey_common_module_notifications',
  reporting: 'lockey_common_module_reporting',
};

/** Module icon used for group header. */
const moduleIcons: Record<string, LucideIcon> = {
  identity: Shield,
  contacts: Contact,
  documents: FileText,
  notifications: Bell,
  reporting: FileBarChart,
};

function NavItem({
  item,
  collapsed,
  namespaces,
  depth = 0,
}: {
  item: AdminNavigationItem;
  collapsed: boolean;
  namespaces: string[];
  depth?: number;
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
            'flex w-full items-center gap-3 rounded-md px-3 py-1.5 text-sm transition-colors',
            'hover:bg-accent hover:text-accent-foreground',
            isActive && 'text-accent-foreground',
            collapsed && 'justify-center px-2',
          )}
        >
          {Icon && <Icon className="h-4 w-4 shrink-0" />}
          {!collapsed && (
            <>
              <span className="flex-1 text-start">{t(item.label)}</span>
              <ChevronDown
                className={cn('h-3 w-3 transition-transform', open && 'rotate-180')}
              />
            </>
          )}
        </button>
        {!collapsed && open && (
          <div className="ms-4 mt-0.5 space-y-0.5 border-s ps-3">
            {item.children.map((child) => (
              <NavItem key={child.path} item={child} collapsed={false} namespaces={namespaces} depth={depth + 1} />
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
          'flex items-center gap-3 rounded-md px-3 py-1.5 text-sm transition-colors',
          'hover:bg-accent hover:text-accent-foreground',
          active && 'bg-accent font-medium text-accent-foreground',
          collapsed && 'justify-center px-2',
          depth > 0 && 'text-muted-foreground',
        )
      }
    >
      {Icon && <Icon className="h-4 w-4 shrink-0" />}
      {!collapsed && <span>{t(item.label)}</span>}
    </NavLink>
  );
}

function ModuleGroup({
  module,
  collapsed,
  namespaces,
}: {
  module: AdminModuleManifest;
  collapsed: boolean;
  namespaces: string[];
}) {
  const { t } = useTranslation('common');
  const location = useLocation();
  const [open, setOpen] = useState(() =>
    module.navigation.some((item) => location.pathname.startsWith(item.path)),
  );

  const GroupIcon = moduleIcons[module.name];
  const displayName = moduleDisplayNames[module.name] ?? module.name;

  if (collapsed) {
    // When collapsed, show only icons for first nav item per module
    return (
      <div className="space-y-0.5">
        {module.navigation.map((item) => (
          <NavItem key={item.path} item={item} collapsed namespaces={namespaces} />
        ))}
      </div>
    );
  }

  return (
    <div>
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="flex w-full items-center gap-2 px-3 py-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground hover:text-foreground transition-colors"
      >
        {GroupIcon && <GroupIcon className="h-3.5 w-3.5" />}
        <span className="flex-1 text-start">{t(displayName)}</span>
        <ChevronDown
          className={cn('h-3 w-3 transition-transform', open && 'rotate-180')}
        />
      </button>
      {open && (
        <div className="space-y-0.5 pb-1">
          {module.navigation.map((item) => (
            <NavItem key={item.path} item={item} collapsed={false} namespaces={namespaces} />
          ))}
        </div>
      )}
    </div>
  );
}

/** Admin sidebar with module-aware navigation and collapse support. */
export function Sidebar() {
  const { t } = useTranslation('common');
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);
  const { activeModules } = useModules();

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
        {/* Dashboard — always visible */}
        <NavItem
          item={{ label: 'lockey_nav_dashboard', path: '/dashboard', icon: 'LayoutDashboard' }}
          collapsed={!sidebarOpen}
          namespaces={namespaces}
        />

        {sidebarOpen && <div className="my-2 border-t" />}

        {/* Module groups */}
        {activeModules.map((module) => (
          <ModuleGroup
            key={module.name}
            module={module}
            collapsed={!sidebarOpen}
            namespaces={namespaces}
          />
        ))}
      </nav>
    </aside>
  );
}
