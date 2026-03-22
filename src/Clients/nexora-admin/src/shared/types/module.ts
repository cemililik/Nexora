import type { LazyExoticComponent, ComponentType } from 'react';

/** Admin module manifest for routing and navigation. */
export interface AdminModuleManifest {
  name: string;
  icon: string;
  routes: AdminRoute[];
  navigation: AdminNavigationItem[];
  permissions: string[];
}

/** Lazy-loaded route for a module page. */
export interface AdminRoute {
  path: string;
  component: LazyExoticComponent<ComponentType>;
}

/** Navigation item rendered in the admin sidebar. */
export interface AdminNavigationItem {
  /** Localization key (lockey_*) for the label. */
  label: string;
  /** Route path relative to admin root (e.g., '/users'). */
  path: string;
  /** Lucide icon name (e.g., 'Users', 'Shield'). */
  icon: string;
  /** Child navigation items for collapsible groups. */
  children?: AdminNavigationItem[];
}

/** Installed module info from backend. */
export interface TenantModuleDto {
  id: string;
  moduleName: string;
  isActive: boolean;
  installedAt: string;
  installedBy?: string;
}
