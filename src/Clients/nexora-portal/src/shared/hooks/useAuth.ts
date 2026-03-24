'use client';

import { signOut, useSession } from 'next-auth/react';
import { useLocale, useTranslations } from 'next-intl';
import { useEffect, useState } from 'react';
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
  const [fetchFailed, setFetchFailed] = useState(false);
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

      // Fetch full user info only if not already loaded and not previously failed
      if (!user && !fetchFailed) {
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
            setFetchFailed(true);
            clearSession();
            toast.error(te('lockey_error_session_expired'));
          });
      }
    } else if (status === 'unauthenticated') {
      setAuthToken(null);
      clearSession();
    }
  }, [session, status, user, fetchFailed, setSession, clearSession, locale, te]);

  useEffect(() => {
    if (status === 'unauthenticated' && fetchFailed) {
      // Defer the state update to avoid cascading renders
      const timer = setTimeout(() => {
        setFetchFailed(false);
      }, 0);
      return () => clearTimeout(timer);
    }
  }, [status, fetchFailed]);

  return {
    user,
    isAuthenticated,
    isLoading: status === 'loading',
    session,
  };
}
