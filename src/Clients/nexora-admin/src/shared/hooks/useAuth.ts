import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

import { api, setAuthToken } from '@/shared/lib/api';
import { createKeycloak, parseTokenClaims } from '@/shared/lib/auth';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import type { UserInfo } from '@/shared/types/auth';

/**
 * Initializes Keycloak authentication and synchronizes session state.
 * Uses PKCE flow with login-required mode — unauthenticated users are
 * redirected to Keycloak login page automatically.
 */
export function useAuth() {
  const { setSession, clearSession, updateToken, user, isAuthenticated, token } =
    useAuthStore();
  const { t } = useTranslation();
  const [isInitializing, setIsInitializing] = useState(true);
  const initRef = useRef(false);

  useEffect(() => {
    // Prevent double-init in React StrictMode
    if (initRef.current) return;
    initRef.current = true;

    const keycloak = createKeycloak();

    keycloak
      .init({ onLoad: 'login-required', pkceMethod: 'S256' })
      .then(async (authenticated) => {
        if (!authenticated || !keycloak.token) {
          clearSession();
          setIsInitializing(false);
          return;
        }

        setAuthToken(keycloak.token);

        try {
          const claims = parseTokenClaims(keycloak.token);
          const userInfo = await api.get<UserInfo>('/identity/users/me');

          setSession({
            user: userInfo,
            token: keycloak.token,
            tenantId: claims.tenant_id,
            organizationId: claims.organization_id,
            permissions: claims.permissions,
          });
        } catch {
          clearSession();
          toast.error(t('lockey_error_session_expired'));
        }

        setIsInitializing(false);
      })
      .catch(() => {
        clearSession();
        setIsInitializing(false);
      });

    keycloak.onTokenExpired = () => {
      keycloak
        .updateToken(30)
        .then(() => {
          if (keycloak.token) {
            setAuthToken(keycloak.token);
            updateToken(keycloak.token);
          }
        })
        .catch(() => {
          clearSession();
          setAuthToken(null);
          keycloak.login();
        });
    };
  }, [setSession, clearSession, updateToken, t]);

  return { user, isAuthenticated, isLoading: isInitializing, token };
}
