'use client';

import { useSession } from 'next-auth/react';
import { useEffect } from 'react';

import { api, setAuthToken } from '@/shared/lib/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import type { UserInfo } from '@/shared/types/auth';

/**
 * Syncs NextAuth session data into Zustand auth store
 * and fetches full user info from the /users/me endpoint.
 */
export function useAuth() {
  const { data: session, status } = useSession();
  const { setSession, clearSession, user, isAuthenticated } = useAuthStore();

  useEffect(() => {
    if (status === 'authenticated' && session?.accessToken) {
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
            // User fetch failed — clear session
            clearSession();
          });
      }
    } else if (status === 'unauthenticated') {
      setAuthToken(null);
      clearSession();
    }
  }, [session, status, user, setSession, clearSession]);

  return {
    user,
    isAuthenticated,
    isLoading: status === 'loading',
    session,
  };
}
