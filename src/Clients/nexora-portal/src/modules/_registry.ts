import type { PortalModuleManifest } from '@/shared/types/module';

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
 * Extension points a manifest can use:
 * - navigation: sidebar entries (aggregated by Sidebar via useModules)
 * - sections: module-owned widgets at fixed positions (dashboard-main,
 *   dashboard-sidebar, profile) — rendered via <SectionRenderer position=... />
 * - slots: named cross-module contributions — host pages render via
 *   <ModuleSlot slotId="..." /> (stacked) or <ModuleTabs slotId="..." />
 *   (tabbed). Example: a Finance module contributing a Payment History tab
 *   to the Contact 360° page via slot id "contact.detail.tabs".
 *
 * See: docs/architecture/MODULE_SYSTEM.md Section 7 (Portal UI Integration)
 */
export const allPortalModules: PortalModuleManifest[] = [
  // donationsManifest,
  // sponsorshipsManifest,
  // eventsManifest,
];
