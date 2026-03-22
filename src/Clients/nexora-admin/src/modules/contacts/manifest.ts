import { lazy } from 'react';

import type { AdminModuleManifest } from '@/shared/types/module';
import { registerModuleLocales } from '@/shared/lib/i18n';
import enContacts from '@/locales/en/contacts.json';
import trContacts from '@/locales/tr/contacts.json';

// Register contacts module translations
registerModuleLocales('contacts', { en: enContacts, tr: trContacts });

export const contactsManifest: AdminModuleManifest = {
  name: 'contacts',
  icon: 'Contact',
  routes: [
    {
      path: 'contacts',
      component: lazy(() => import('./pages/ContactListPage')),
    },
    {
      path: 'contacts/create',
      component: lazy(() => import('./pages/ContactCreatePage')),
    },
    {
      path: 'contacts/:id',
      component: lazy(() => import('./pages/ContactDetailPage')),
    },
    {
      path: 'tags',
      component: lazy(() => import('./pages/TagManagementPage')),
    },
    {
      path: 'custom-fields',
      component: lazy(() => import('./pages/CustomFieldManagementPage')),
    },
    {
      path: 'import',
      component: lazy(() => import('./pages/ImportPage')),
    },
    {
      path: 'export',
      component: lazy(() => import('./pages/ExportPage')),
    },
  ],
  navigation: [
    {
      label: 'lockey_contacts_nav_contacts',
      path: '/contacts/contacts',
      icon: 'Contact',
    },
    {
      label: 'lockey_contacts_nav_tags',
      path: '/contacts/tags',
      icon: 'Tag',
    },
    {
      label: 'lockey_contacts_nav_custom_fields',
      path: '/contacts/custom-fields',
      icon: 'Blocks',
    },
    {
      label: 'lockey_contacts_nav_import_export',
      path: '/contacts/import',
      icon: 'FolderOpen',
      children: [
        {
          label: 'lockey_contacts_nav_import',
          path: '/contacts/import',
          icon: 'FolderOpen',
        },
        {
          label: 'lockey_contacts_nav_export',
          path: '/contacts/export',
          icon: 'FolderOpen',
        },
      ],
    },
  ],
  permissions: [
    'contacts.contact.read',
    'contacts.contact.create',
    'contacts.contact.update',
    'contacts.contact.delete',
    'contacts.tag.read',
    'contacts.tag.create',
    'contacts.tag.update',
    'contacts.tag.delete',
    'contacts.custom-field.read',
    'contacts.custom-field.manage',
    'contacts.import',
    'contacts.export',
    'contacts.gdpr.export',
    'contacts.gdpr.delete',
    'contacts.merge',
  ],
};
