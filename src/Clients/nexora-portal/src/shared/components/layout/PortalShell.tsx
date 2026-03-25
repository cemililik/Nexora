'use client';

import { type ReactNode, useRef, useEffect } from 'react';

import { useAuth } from '@/shared/hooks/useAuth';
import { setAuthToken } from '@/shared/lib/api';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import type { UserInfo } from '@/shared/types/auth';

import { PortalLayout } from './PortalLayout';

interface PortalShellProps {
  children: ReactNode;
  serverUser?: UserInfo | null;
  serverTenantId?: string | null;
  serverOrganizationId?: string | null;
  serverPermissions?: string[] | null;
  serverAccessToken?: string | null;
}

/**
 * Client component wrapper that initializes auth state and renders the portal layout.
 *
 * Populates Zustand synchronously before first render (hydration-safe).
 * Sets API auth token in useEffect (side effect, runs after hydration).
 */
export function PortalShell({
  children,
  serverUser,
  serverTenantId,
  serverOrganizationId,
  serverPermissions,
  serverAccessToken,
}: PortalShellProps) {
  const initialized = useRef(false);

  // Synchronous Zustand initialization — before first render
  if (!initialized.current && serverUser) {
    initialized.current = true;
    useAuthStore.getState().setSession({
      user: serverUser,
      tenantId: serverTenantId ?? '',
      organizationId: serverOrganizationId ?? undefined,
      permissions: serverPermissions ?? [],
    });
  }

  // Set API token after mount — ensures client-side API calls include auth header
  useEffect(() => {
    if (serverAccessToken) {
      setAuthToken(serverAccessToken);
    }
  }, [serverAccessToken]);

  useAuth();

  return <PortalLayout>{children}</PortalLayout>;
}

PortalShell.displayName = 'PortalShell';
