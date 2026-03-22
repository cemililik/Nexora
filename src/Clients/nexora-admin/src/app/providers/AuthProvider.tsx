import type { ReactNode } from 'react';

import { useAuth } from '@/shared/hooks/useAuth';
import i18n from '@/shared/lib/i18n';

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
        aria-label={i18n.t('lockey_common_loading', { ns: 'common' })}
      >
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-muted border-t-primary" />
      </div>
    );
  }

  return <>{children}</>;
}
