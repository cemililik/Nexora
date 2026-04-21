import type { LazyExoticComponent, FC } from 'react';

/** Portal module manifest for navigation, section, and cross-module slot registration. */
export interface PortalModuleManifest {
  name: string;
  navigation: PortalNavigationItem[];
  permissions: string[];
  sections?: PortalSection[];
  /**
   * Named extension slots contributed by this module to other modules' pages.
   * Key is the slot identifier the host page renders via `<ModuleSlot slotId="..." />`
   * or `<ModuleTabs slotId="..." />`. Example: a Finance module contributing a
   * `"contact.detail.tabs"` entry to add a Payment History tab to the Contact 360° view.
   */
  slots?: Record<string, PortalSlotContribution[]>;
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
  component: LazyExoticComponent<FC>;
  permissions: string[];
}

/**
 * Contribution from a module to a named extension slot.
 * Rendered by `<ModuleSlot />` (stacked) or `<ModuleTabs />` (tabbed) in a host page.
 */
export interface PortalSlotContribution {
  id: string;
  order: number;
  permissions: string[];
  component: LazyExoticComponent<FC>;
  /** Optional lockey_ key for the contribution label (used by `<ModuleTabs />`). */
  labelKey?: string;
}

/** Installed module info from backend. */
export interface TenantModuleDto {
  id: string;
  moduleName: string;
  isActive: boolean;
  installedAt: string;
  installedBy?: string;
}
