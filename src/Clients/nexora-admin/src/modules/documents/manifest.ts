import { lazy } from 'react';

import type { AdminModuleManifest } from '@/shared/types/module';
import { registerModuleLocales } from '@/shared/lib/i18n';
import enDocuments from '@/locales/en/documents.json';
import trDocuments from '@/locales/tr/documents.json';

// Register documents module translations
registerModuleLocales('documents', { en: enDocuments, tr: trDocuments });

export const documentsManifest: AdminModuleManifest = {
  name: 'documents',
  icon: 'FileText',
  routes: [
    {
      path: 'documents',
      component: lazy(() => import('./pages/DocumentListPage')),
    },
    {
      path: 'documents/:id',
      component: lazy(() => import('./pages/DocumentDetailPage')),
    },
    {
      path: 'folders',
      component: lazy(() => import('./pages/FolderManagementPage')),
    },
    {
      path: 'templates',
      component: lazy(() => import('./pages/TemplateListPage')),
    },
    {
      path: 'templates/:id',
      component: lazy(() => import('./pages/TemplateDetailPage')),
    },
    {
      path: 'signatures',
      component: lazy(() => import('./pages/SignatureListPage')),
    },
    {
      path: 'signatures/create',
      component: lazy(() => import('./pages/SignatureCreatePage')),
    },
    {
      path: 'signatures/:id',
      component: lazy(() => import('./pages/SignatureDetailPage')),
    },
  ],
  navigation: [
    {
      label: 'lockey_documents_nav_documents',
      path: '/documents/documents',
      icon: 'FileText',
    },
    {
      label: 'lockey_documents_nav_folders',
      path: '/documents/folders',
      icon: 'FolderOpen',
    },
    {
      label: 'lockey_documents_nav_templates',
      path: '/documents/templates',
      icon: 'Copy',
    },
    {
      label: 'lockey_documents_nav_signatures',
      path: '/documents/signatures',
      icon: 'PenTool',
    },
  ],
  permissions: [
    'documents.document.read',
    'documents.document.upload',
    'documents.document.update',
    'documents.document.delete',
    'documents.folder.read',
    'documents.folder.manage',
    'documents.signature.read',
    'documents.signature.create',
    'documents.signature.manage',
    'documents.template.read',
    'documents.template.manage',
  ],
};
