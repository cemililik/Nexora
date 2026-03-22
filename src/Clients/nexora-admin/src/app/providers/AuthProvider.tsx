import type { ReactNode } from 'react';

import { useAuth } from '@/shared/hooks/useAuth';

interface AuthProviderProps {
  children: ReactNode;
}

/**
 * Auth provider that initializes Keycloak and shows a loading state
 * until authentication is resolved.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const { isLoading } = useAuth();

  if (isLoading) {
    return (
      <div
        className="flex min-h-screen items-center justify-center"
        role="status"
        aria-label="Loading"
      >
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-muted border-t-primary" />
      </div>
    );
  }

  return <>{children}</>;
}
