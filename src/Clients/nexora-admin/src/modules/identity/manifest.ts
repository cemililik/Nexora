import { lazy } from 'react';

import type { AdminModuleManifest } from '@/shared/types/module';
import { registerModuleLocales } from '@/shared/lib/i18n';
import enIdentity from '@/locales/en/identity.json';
import trIdentity from '@/locales/tr/identity.json';

// Register identity module translations
registerModuleLocales('identity', { en: enIdentity, tr: trIdentity });

export const identityManifest: AdminModuleManifest = {
  name: 'identity',
  icon: 'Shield',
  routes: [
    {
      path: 'users',
      component: lazy(() => import('./pages/UserListPage')),
    },
    {
      path: 'users/create',
      component: lazy(() => import('./pages/UserCreatePage')),
    },
    {
      path: 'users/:id',
      component: lazy(() => import('./pages/UserDetailPage')),
    },
    {
      path: 'roles',
      component: lazy(() => import('./pages/RoleListPage')),
    },
    {
      path: 'roles/:id',
      component: lazy(() => import('./pages/RoleDetailPage')),
    },
    {
      path: 'organizations',
      component: lazy(() => import('./pages/OrganizationListPage')),
    },
    {
      path: 'organizations/create',
      component: lazy(() => import('./pages/OrganizationCreatePage')),
    },
    {
      path: 'organizations/:id',
      component: lazy(() => import('./pages/OrganizationDetailPage')),
    },
    {
      path: 'tenants',
      component: lazy(() => import('./pages/TenantListPage')),
    },
    {
      path: 'tenants/create',
      component: lazy(() => import('./pages/TenantCreatePage')),
    },
    {
      path: 'tenants/:id',
      component: lazy(() => import('./pages/TenantDetailPage')),
    },
    {
      path: 'audit-logs',
      component: lazy(() => import('./pages/AuditLogPage')),
    },
  ],
  navigation: [
    {
      label: 'lockey_identity_nav_users',
      path: '/identity/users',
      icon: 'Users',
    },
    {
      label: 'lockey_identity_nav_roles',
      path: '/identity/roles',
      icon: 'ShieldCheck',
    },
    {
      label: 'lockey_identity_nav_organizations',
      path: '/identity/organizations',
      icon: 'Building2',
    },
    {
      label: 'lockey_identity_nav_tenants',
      path: '/identity/tenants',
      icon: 'Server',
    },
    {
      label: 'lockey_identity_nav_audit_logs',
      path: '/identity/audit-logs',
      icon: 'FileText',
    },
  ],
  permissions: [
    'identity.users.read',
    'identity.users.create',
    'identity.users.update',
    'identity.users.delete',
    'identity.roles.read',
    'identity.roles.create',
    'identity.roles.update',
    'identity.roles.delete',
    'identity.organizations.read',
    'identity.organizations.create',
    'identity.organizations.update',
    'identity.organizations.delete',
    'identity.tenants.read',
    'identity.tenants.create',
    'identity.tenants.update',
    'identity.tenants.delete',
    'identity.audit-logs.read',
    'identity.modules.read',
    'identity.modules.manage',
  ],
};
