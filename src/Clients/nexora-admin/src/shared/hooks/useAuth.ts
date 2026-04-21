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

        // Session is ready — release the loading gate immediately. Locale resolution runs
        // in the background so a slow tenant/org fetch cannot keep the user on the spinner.
        setIsInitializing(false);

        void resolveLocale(claims).then(setTenantLocale).catch(() => {
          // Locale features degrade to i18n.language defaults.
        });
      })
      .catch(() => {
        clearSession();
        setIsInitializing(false);
      });

  }, [setSession, clearSession, updateToken, setTenantLocale]);

  return { user, isAuthenticated, isLoading: isInitializing };
}

interface TenantLocaleResponse {
  defaultLocale: string;
  defaultCurrency: string;
  defaultTimezone: string;
  defaultDocumentLanguage: string;
}

interface OrganizationLocaleResponse {
  defaultLocale: string;
  defaultCurrency: string;
  timezone: string;
  defaultLanguage: string;
}

interface ResolvedTenantLocale {
  locale: string;
  currency: string;
  timezone: string;
  documentLanguage: string;
}

async function resolveLocale(claims: {
  tenant_id: string;
  organization_id?: string;
}): Promise<ResolvedTenantLocale> {
  // Tenant and organization locale reads are independent — fetch both in parallel.
  const [tenantResult, orgResult] = await Promise.all([
    api.get<TenantLocaleResponse>(
      `/identity/tenants/${encodeURIComponent(claims.tenant_id)}`,
    ),
    claims.organization_id
      ? api
          .get<OrganizationLocaleResponse>(
            `/identity/organizations/${encodeURIComponent(claims.organization_id)}`,
          )
          .catch(() => null)
      : Promise.resolve(null),
  ]);

  // Org settings override tenant settings when the user belongs to an org (3-tier model).
  if (orgResult) {
    return {
      locale: orgResult.defaultLocale,
      currency: orgResult.defaultCurrency,
      timezone: orgResult.timezone,
      documentLanguage: orgResult.defaultLanguage,
    };
  }

  return {
    locale: tenantResult.defaultLocale,
    currency: tenantResult.defaultCurrency,
    timezone: tenantResult.defaultTimezone,
    documentLanguage: tenantResult.defaultDocumentLanguage,
  };
}
