import type { ComponentType, LazyExoticComponent } from 'react';

/** Portal module manifest for navigation and section registration. */
export interface PortalModuleManifest {
  name: string;
  navigation: PortalNavigationItem[];
  permissions: string[];
  sections?: PortalSection[];
}

/** Navigation item rendered in the sidebar. */
export interface PortalNavigationItem {
  /** Localization key (lockey_*) for the label. */
  label: string;
  /** Route path relative to portal root (e.g., '/donations'). */
  path: string;
  /** Lucide icon name (e.g., 'Heart', 'Users'). */
  icon: string;
}

/** Section position slots available in the portal. */
export type SectionPosition =
  | 'dashboard-main'
  | 'dashboard-sidebar'
  | 'profile';

/** Module-contributed section for the page builder infrastructure. */
export interface PortalSection {
  id: string;
  position: SectionPosition;
  order: number;
  component: LazyExoticComponent<ComponentType>;
  permissions: string[];
}

/** Installed module info from backend. */
export interface TenantModuleDto {
  id: string;
  moduleName: string;
  isActive: boolean;
  installedAt: string;
  installedBy?: string;
}
