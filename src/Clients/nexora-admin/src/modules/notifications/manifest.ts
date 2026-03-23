import { lazy } from 'react';

import type { AdminModuleManifest } from '@/shared/types/module';
import { registerModuleLocales } from '@/shared/lib/i18n';
import enNotifications from '@/locales/en/notifications.json';
import trNotifications from '@/locales/tr/notifications.json';

// Register notifications module translations
registerModuleLocales('notifications', { en: enNotifications, tr: trNotifications });

export const notificationsManifest: AdminModuleManifest = {
  name: 'notifications',
  icon: 'Bell',
  routes: [
    {
      path: 'notifications',
      component: lazy(() => import('./pages/NotificationListPage')),
    },
    {
      path: 'notifications/:id',
      component: lazy(() => import('./pages/NotificationDetailPage')),
    },
    {
      path: 'notifications/send',
      component: lazy(() => import('./pages/SendNotificationPage')),
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
      path: 'providers',
      component: lazy(() => import('./pages/ProviderListPage')),
    },
    {
      path: 'schedule',
      component: lazy(() => import('./pages/ScheduleListPage')),
    },
  ],
  navigation: [
    {
      label: 'lockey_notifications_nav_notifications',
      path: '/notifications/notifications',
      icon: 'Bell',
    },
    {
      label: 'lockey_notifications_nav_templates',
      path: '/notifications/templates',
      icon: 'FileCode',
    },
    {
      label: 'lockey_notifications_nav_providers',
      path: '/notifications/providers',
      icon: 'Server',
    },
    {
      label: 'lockey_notifications_nav_schedule',
      path: '/notifications/schedule',
      icon: 'Clock',
    },
  ],
  permissions: [
    'notifications.notification.read',
    'notifications.notification.send',
    'notifications.template.read',
    'notifications.template.manage',
    'notifications.provider.read',
    'notifications.provider.manage',
    'notifications.schedule.read',
    'notifications.schedule.manage',
  ],
};
