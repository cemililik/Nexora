import type { PortalModuleManifest } from '@/shared/types/module';

/**
 * Registry of all portal module manifests.
 * Add new module manifests here as they are implemented.
 *
 * Each module contributes:
 * - navigation: sidebar links
 * - permissions: required permissions to access the module
 * - sections: dashboard widgets and page builder slots
 */
export const allPortalModules: PortalModuleManifest[] = [
  // Module manifests will be added here as portal modules are built.
  // Example:
  // donationsManifest,
  // sponsorshipsManifest,
  // eventsManifest,
];
