'use client';

import { signOut, useSession } from 'next-auth/react';
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

  useEffect(() => {
    // Handle refresh token errors — force re-login
    if (session?.error === 'RefreshAccessTokenError') {
      signOut({ callbackUrl: '/auth/login' });
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
            toast.error('lockey_error_session_expired');
          });
      }
    } else if (status === 'unauthenticated') {
      setAuthToken(null);
      clearSession();
      setFetchFailed(false);
    }
  }, [session, status, user, fetchFailed, setSession, clearSession]);

  return {
    user,
    isAuthenticated,
    isLoading: status === 'loading',
    session,
  };
}
