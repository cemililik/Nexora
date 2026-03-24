import type { AdminModuleManifest } from '@/shared/types/module';
import { identityManifest } from '@/modules/identity/manifest';
import { contactsManifest } from '@/modules/contacts/manifest';
import { documentsManifest } from '@/modules/documents/manifest';
import { notificationsManifest } from '@/modules/notifications/manifest';
import { reportingManifest } from '@/modules/reporting/manifest';

/**
 * Admin Module Registry
 *
 * Add module manifests here as admin modules are implemented.
 * Each entry must implement AdminModuleManifest (src/shared/types/module.ts).
 *
 * Pattern:
 * 1. Create: src/modules/{moduleName}/manifest.ts
 * 2. Export: export const {moduleName}Manifest: AdminModuleManifest = { ... }
 * 3. Register: import here and add to allAdminModules array
 *
 * See: docs/architecture/MODULE_SYSTEM.md Section 7 (Admin UI Integration)
 */
export const allAdminModules: AdminModuleManifest[] = [
  identityManifest,
  contactsManifest,
  documentsManifest,
  notificationsManifest,
  reportingManifest,
];
