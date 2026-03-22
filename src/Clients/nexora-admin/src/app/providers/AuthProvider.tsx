import type { ReactNode } from 'react';

import { useAuth } from '@/shared/hooks/useAuth';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';

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
      <div className="flex min-h-screen items-center justify-center">
        <LoadingSkeleton />
      </div>
    );
  }

  return children;
}
