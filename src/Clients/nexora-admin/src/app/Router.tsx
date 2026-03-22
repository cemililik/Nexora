import { lazy, Suspense, type ReactNode } from 'react';
import { createBrowserRouter, Navigate } from 'react-router';
import { useTranslation } from 'react-i18next';

import { AppLayout } from '@/shared/components/layout/AppLayout';
import { RequireAuth } from '@/shared/components/guards/RequireAuth';
import { RequirePermission } from '@/shared/components/guards/RequirePermission';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { ErrorBoundary } from '@/shared/components/feedback/ErrorBoundary';
import { useModules } from '@/shared/hooks/useModules';
import { allAdminModules } from '@/modules/_registry';

const LoginPage = lazy(() => import('./pages/LoginPage'));
const DashboardPage = lazy(() => import('./pages/DashboardPage'));
const NotFoundPage = lazy(() => import('./pages/NotFoundPage'));

/** Guard that checks whether a module is installed for the current tenant. */
function RequireModule({ moduleName, children }: { moduleName: string; children: ReactNode }) {
  const { t } = useTranslation('common');
  const { hasModule, isLoading } = useModules();

  if (isLoading) {
    return <LoadingSkeleton />;
  }

  if (!hasModule(moduleName)) {
    return (
      <div className="flex items-center justify-center p-8 text-muted-foreground">
        {t('lockey_common_module_not_installed')}
      </div>
    );
  }

  return <>{children}</>;
}

// Build module routes dynamically from registry
const moduleRoutes = allAdminModules.flatMap((mod) =>
  mod.routes.map((route) => ({
    path: `${mod.name}/${route.path}`,
    element: (
      <ErrorBoundary>
        <Suspense fallback={<LoadingSkeleton />}>
          <RequireModule moduleName={mod.name}>
            <RequirePermission required={mod.permissions} mode="any">
              <route.component />
            </RequirePermission>
          </RequireModule>
        </Suspense>
      </ErrorBoundary>
    ),
  })),
);

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <Navigate to="/dashboard" replace /> },
      { path: 'dashboard', element: <DashboardPage /> },
      ...moduleRoutes,
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
