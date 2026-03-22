import { lazy } from 'react';
import { createBrowserRouter, Navigate } from 'react-router';

import { AppLayout } from '@/shared/components/layout/AppLayout';
import { RequireAuth } from '@/shared/components/guards/RequireAuth';

const LoginPage = lazy(() => import('./pages/LoginPage'));
const DashboardPage = lazy(() => import('./pages/DashboardPage'));
const NotFoundPage = lazy(() => import('./pages/NotFoundPage'));

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
      // Module routes will be injected here in Phase B+
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
