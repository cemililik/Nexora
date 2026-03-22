import type { ReactNode } from 'react';
import { Navigate } from 'react-router';

import { useAuthStore } from '@/shared/lib/stores/authStore';

interface RequireAuthProps {
  children: ReactNode;
  fallback?: ReactNode;
}

/**
 * Guard component that redirects unauthenticated users to login.
 * Shows a loading spinner while auth state is being determined.
 */
export function RequireAuth({ children, fallback }: RequireAuthProps) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);

  if (!isAuthenticated) {
    if (fallback) return <>{fallback}</>;

    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}
