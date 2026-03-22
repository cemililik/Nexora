import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router';

import { AppLayout } from '@/shared/components/layout/AppLayout';
import { RequireAuth } from '@/shared/components/guards/RequireAuth';
import { RequirePermission } from '@/shared/components/guards/RequirePermission';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { ErrorBoundary } from '@/shared/components/feedback/ErrorBoundary';
import { allAdminModules } from '@/modules/_registry';

const LoginPage = lazy(() => import('./pages/LoginPage'));
const DashboardPage = lazy(() => import('./pages/DashboardPage'));
const NotFoundPage = lazy(() => import('./pages/NotFoundPage'));

// Build module routes dynamically from registry
const moduleRoutes = allAdminModules.flatMap((mod) =>
  mod.routes.map((route) => ({
    path: `${mod.name}/${route.path}`,
    element: (
      <ErrorBoundary>
        <Suspense fallback={<LoadingSkeleton />}>
          <RequirePermission required={mod.permissions} mode="any">
            <route.component />
          </RequirePermission>
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
