'use client';

import { useTranslations } from 'next-intl';
import { signIn } from 'next-auth/react';

/**
 * Login page — initiates Keycloak OIDC flow.
 * In the future, this can include an organization slug input
 * for multi-tenant realm resolution.
 */
export default function LoginPage() {
  const t = useTranslations();

  return (
    <div className="flex min-h-screen items-center justify-center bg-background">
      <div className="w-full max-w-md space-y-6 rounded-lg border border-border p-8">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-foreground">
            {t('lockey_common_app_name')}
          </h1>
        </div>

        <button
          onClick={() => signIn('keycloak', { callbackUrl: '/dashboard' })}
          className="w-full rounded-md bg-accent px-4 py-3 text-sm font-medium text-accent-foreground hover:bg-accent/90 transition-colors"
        >
          {t('lockey_common_sign_in')}
        </button>
      </div>
    </div>
  );
}
