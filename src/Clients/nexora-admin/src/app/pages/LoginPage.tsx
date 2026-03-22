import { useTranslation } from 'react-i18next';
import { Navigate } from 'react-router';

import { useAuthStore } from '@/shared/lib/stores/authStore';

/**
 * Login page — redirects to dashboard if already authenticated.
 * Keycloak handles the actual login flow via AuthProvider.
 */
export default function LoginPage() {
  const { t } = useTranslation();
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }

  return (
    <div
      className="flex min-h-screen items-center justify-center"
      role="status"
      aria-label={t('lockey_common_loading')}
    >
      <div className="text-center">
        <div className="mx-auto mb-4 h-8 w-8 animate-spin rounded-full border-4 border-muted border-t-primary" />
        <p className="text-sm text-muted-foreground">
          {t('lockey_common_loading')}
        </p>
      </div>
    </div>
  );
}
