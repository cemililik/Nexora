import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';

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
  const { setSession, clearSession, updateToken, user, isAuthenticated } =
    useAuthStore();
  const { t } = useTranslation();
  const tRef = useRef<TFunction>(t);
  tRef.current = t;
  const [isInitializing, setIsInitializing] = useState(true);
  const initRef = useRef(false);

  useEffect(() => {
    // Prevent double-init in React StrictMode
    if (initRef.current) return;
    initRef.current = true;

    const keycloak = createKeycloak();

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
          setAuthToken(null);
          clearSession();
          keycloak.login();
        });
    };

    keycloak
      .init({ onLoad: 'login-required', pkceMethod: 'S256' })
      .then(async (authenticated) => {
        if (!authenticated || !keycloak.token) {
          clearSession();
          setIsInitializing(false);
          return;
        }

        setAuthToken(keycloak.token);

        const claims = parseTokenClaims(keycloak.token);

        let userInfo: UserInfo | null = null;
        try {
          userInfo = await api.get<UserInfo>('/identity/users/me');
        } catch (err: unknown) {
          const status = (err as { response?: { status?: number } })?.response?.status;
          if (status === 401 || status === 403) {
            // Token rejected by backend — force re-login
            console.error('[useAuth] /me returned', status, '— redirecting to login');
            setAuthToken(null);
            clearSession();
            setIsInitializing(false);
            keycloak.login();
            return;
          }
          // 404 (tenant not provisioned), network errors, 5xx — fall back to token claims
          console.warn('[useAuth] /me failed, falling back to token claims', err);
        }

        setSession({
          user: userInfo ?? {
            id: claims.sub,
            email: claims.email,
            firstName: claims.preferred_username,
            lastName: '',
            status: 'Active',
            organizations: [],
          },
          token: keycloak.token,
          tenantId: claims.tenant_id,
          organizationId: claims.organization_id,
          permissions: claims.permissions,
        });

        setIsInitializing(false);
      })
      .catch(() => {
        clearSession();
        setIsInitializing(false);
      });

  }, [setSession, clearSession, updateToken]);

  return { user, isAuthenticated, isLoading: isInitializing };
}
