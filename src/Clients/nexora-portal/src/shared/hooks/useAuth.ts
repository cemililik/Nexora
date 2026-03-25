'use client';

import { signOut, useSession } from 'next-auth/react';
import { useLocale, useTranslations } from 'next-intl';
import { useEffect, useRef } from 'react';
import { toast } from 'sonner';

import { api, setAuthToken } from '@/shared/lib/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import type { UserInfo } from '@/shared/types/auth';

/**
 * Syncs NextAuth session data into Zustand auth store
 * and fetches full user info from the /users/me endpoint.
 *
 * PortalShell already populates Zustand with server-fetched user data
 * before first render. This hook handles token refresh and re-login only.
 * It will NOT clear the store if useSession encounters a transient fetch error
 * (ClientFetchError) — as long as user data exists in the store.
 */
export function useAuth() {
  const { data: session, status } = useSession();
  const { setSession, clearSession, user, isAuthenticated } = useAuthStore();
  const locale = useLocale();
  const te = useTranslations('error');
  const hasInitialized = useRef(false);

  useEffect(() => {
    // Handle explicit refresh token errors — force re-login
    if (session?.error === 'RefreshAccessTokenError') {
      signOut({ callbackUrl: `/${locale}/auth/login` });
      return;
    }

    if (status === 'authenticated' && session?.accessToken) {
      hasInitialized.current = true;
      setAuthToken(session.accessToken);

      // Fetch full user info only if not already loaded
      if (!user) {
        api
          .get<UserInfo>('/identity/users/me')
          .then((userInfo) => {
            setSession({
              user: userInfo,
              tenantId: session.tenantId ?? '',
              organizationId: session.organizationId,
              permissions: session.permissions ?? [],
            });
          })
          .catch(() => {
            // Don't clear if PortalShell already provided user data
            if (!useAuthStore.getState().user) {
              toast.error(te('lockey_error_session_expired'));
            }
          });
      }
    } else if (status === 'unauthenticated' && hasInitialized.current && !useAuthStore.getState().user) {
      // Only clear if we previously had a valid session and user data is gone
      setAuthToken(null);
      clearSession();
    }
  }, [session, status, user, setSession, clearSession, locale, te]);

  return {
    user,
    isAuthenticated,
    isLoading: status === 'loading',
    session,
  };
}
