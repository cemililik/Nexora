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
 * See: docs/architecture/MODULE_SYSTEM.md Section 7 (Portal UI Integration)
 */
export const allPortalModules: PortalModuleManifest[] = [
  // donationsManifest,
  // sponsorshipsManifest,
  // eventsManifest,
];
