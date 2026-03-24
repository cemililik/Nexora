import { lazy } from 'react';

import type { AdminModuleManifest } from '@/shared/types/module';
import { registerModuleLocales } from '@/shared/lib/i18n';
import enReporting from '@/locales/en/reporting.json';
import trReporting from '@/locales/tr/reporting.json';

registerModuleLocales('reporting', { en: enReporting, tr: trReporting });

export const reportingManifest: AdminModuleManifest = {
  name: 'reporting',
  icon: 'BarChart3',
  routes: [
    {
      path: 'reports',
      component: lazy(() => import('./pages/ReportListPage')),
    },
    {
      path: 'reports/:id',
      component: lazy(() => import('./pages/ReportDetailPage')),
    },
    {
      path: 'schedules',
      component: lazy(() => import('./pages/ReportScheduleListPage')),
    },
    {
      path: 'dashboards',
      component: lazy(() => import('./pages/DashboardListPage')),
    },
    {
      path: 'dashboards/:id',
      component: lazy(() => import('./pages/DashboardViewPage')),
    },
  ],
  navigation: [
    {
      label: 'lockey_reporting_nav_reports',
      path: '/reporting/reports',
      icon: 'FileBarChart',
    },
    {
      label: 'lockey_reporting_nav_schedules',
      path: '/reporting/schedules',
      icon: 'Clock',
    },
    {
      label: 'lockey_reporting_nav_dashboards',
      path: '/reporting/dashboards',
      icon: 'LayoutDashboard',
    },
  ],
  permissions: [
    'reporting.definition.read',
    'reporting.definition.manage',
    'reporting.execution.run',
    'reporting.execution.read',
    'reporting.schedule.manage',
    'reporting.dashboard.read',
    'reporting.dashboard.manage',
  ],
};
