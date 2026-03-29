import { lazy } from 'react';

import type { AdminModuleManifest } from '@/shared/types/module';
import { registerModuleLocales } from '@/shared/lib/i18n';
import enAudit from '@/locales/en/audit.json';
import trAudit from '@/locales/tr/audit.json';

// Register audit module translations
registerModuleLocales('audit', { en: enAudit, tr: trAudit });

export const auditManifest: AdminModuleManifest = {
  name: 'audit',
  icon: 'FileSearch',
  routes: [
    {
      path: 'logs',
      component: lazy(() => import('./pages/AuditLogListPage')),
    },
    {
      path: 'logs/:id',
      component: lazy(() => import('./pages/AuditLogDetailPage')),
    },
    {
      path: 'settings',
      component: lazy(() => import('./pages/AuditSettingsPage')),
    },
  ],
  navigation: [
    {
      label: 'lockey_audit_nav_logs',
      path: '/audit/logs',
      icon: 'FileSearch',
    },
    {
      label: 'lockey_audit_nav_settings',
      path: '/audit/settings',
      icon: 'Settings',
      permission: 'audit.settings.read',
    },
  ],
  permissions: [
    'audit.logs.read',
    'audit.settings.read',
    'audit.settings.manage',
  ],
};
