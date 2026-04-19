import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import i18n from '@/shared/lib/i18n';

import axios from 'axios';
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
  const { setSession, clearSession, updateToken, setTenantLocale, user, isAuthenticated } =
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
          const status = axios.isAxiosError(err) ? err.response?.status : undefined;
          if (status === 401 || status === 403) {
            // Token rejected by backend — force re-login
            if (import.meta.env.DEV) console.error('[useAuth] /me returned', status, '— redirecting to login');
            setAuthToken(null);
            clearSession();
            setIsInitializing(false);
            keycloak.login();
            return;
          }
          // 404 (tenant not provisioned), network errors, 5xx — fall back to token claims
          if (import.meta.env.DEV) console.warn('[useAuth] /me failed, falling back to token claims', err);
        }

        // Apply user's stored language preference so the UI renders in their language immediately.
        if (userInfo?.preferredLanguage) {
          void i18n.changeLanguage(userInfo.preferredLanguage);
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
          permissions: userInfo?.permissions ?? claims.permissions,
        });

        // Fetch tenant locale settings (non-critical — gracefully degrades)
        try {
          const tenant = await api.get<{
            defaultLocale: string;
            defaultCurrency: string;
            defaultTimezone: string;
            defaultDocumentLanguage: string;
          }>(`/identity/tenants/${encodeURIComponent(claims.tenant_id)}`);

          // Start with tenant-level locale as the base
          let resolvedLocale = {
            locale: tenant.defaultLocale,
            currency: tenant.defaultCurrency,
            timezone: tenant.defaultTimezone,
            documentLanguage: tenant.defaultDocumentLanguage,
          };

          // Org settings override tenant settings when the user belongs to an org (3-tier model)
          if (claims.organization_id) {
            try {
              const org = await api.get<{
                defaultLocale: string;
                defaultCurrency: string;
                timezone: string;
                defaultLanguage: string;
              }>(`/identity/organizations/${encodeURIComponent(claims.organization_id)}`);
              resolvedLocale = {
                locale: org.defaultLocale,
                currency: org.defaultCurrency,
                timezone: org.timezone,
                documentLanguage: org.defaultLanguage,
              };
            } catch {
              // Org fetch failed — keep tenant locale
            }
          }

          setTenantLocale(resolvedLocale);
        } catch {
          // Locale features degrade to i18n.language defaults
        }

        setIsInitializing(false);
      })
      .catch(() => {
        clearSession();
        setIsInitializing(false);
      });

  }, [setSession, clearSession, updateToken, setTenantLocale]);

  return { user, isAuthenticated, isLoading: isInitializing };
}
