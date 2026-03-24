'use client';

import { signOut, useSession } from 'next-auth/react';
import { useLocale, useTranslations } from 'next-intl';
import { useEffect } from 'react';
import { toast } from 'sonner';

import { api, setAuthToken } from '@/shared/lib/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import type { UserInfo } from '@/shared/types/auth';

/**
 * Syncs NextAuth session data into Zustand auth store
 * and fetches full user info from the /users/me endpoint.
 * Handles refresh token errors by redirecting to login.
 */
export function useAuth() {
  const { data: session, status } = useSession();
  const { setSession, clearSession, user, isAuthenticated } = useAuthStore();
  const locale = useLocale();
  const te = useTranslations('error');

  useEffect(() => {
    // Handle refresh token errors — force re-login
    if (session?.error === 'RefreshAccessTokenError') {
      signOut({ callbackUrl: `/${locale}/auth/login` });
      return;
    }

    if (status === 'authenticated' && session?.accessToken) {
      setAuthToken(session.accessToken);

      // Fetch full user info only if not already loaded
      // Will automatically retry on fresh session (when user changes)
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
            // On error, clear session to force re-login
            // User can retry by logging out and back in, which creates a fresh session
            clearSession();
            toast.error(te('lockey_error_session_expired'));
          });
      }
    } else if (status === 'unauthenticated') {
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
