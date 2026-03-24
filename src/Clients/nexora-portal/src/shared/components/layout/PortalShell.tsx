'use client';

import { type ReactNode, useRef } from 'react';

import { useAuth } from '@/shared/hooks/useAuth';
import { useAuthStore } from '@/shared/lib/stores/authStore';
import type { UserInfo } from '@/shared/types/auth';

import { PortalLayout } from './PortalLayout';

interface PortalShellProps {
  children: ReactNode;
  /** User info fetched server-side in the portal layout. */
  serverUser?: UserInfo | null;
  serverTenantId?: string | null;
  serverOrganizationId?: string | null;
  serverPermissions?: string[] | null;
}

/**
 * Client component wrapper that initializes auth state and renders the portal layout.
 *
 * When serverUser is provided (fetched in the server layout), Zustand is populated
 * synchronously before the first render — eliminating hydration mismatch between
 * server (user data available) and client (Zustand initially empty).
 *
 * useAuth() still runs for token refresh error handling, but skips the /me fetch
 * because the user is already in the store.
 */
export function PortalShell({
  children,
  serverUser,
  serverTenantId,
  serverOrganizationId,
  serverPermissions,
}: PortalShellProps) {
  const initialized = useRef(false);

  // Synchronous Zustand initialization — runs during render, before commit.
  // This ensures the store is populated for the first client render, matching
  // what the server rendered with the same user data.
  if (!initialized.current && serverUser) {
    initialized.current = true;
    useAuthStore.getState().setSession({
      user: serverUser,
      tenantId: serverTenantId ?? '',
      organizationId: serverOrganizationId ?? undefined,
      permissions: serverPermissions ?? [],
    });
  }

  useAuth();

  return <PortalLayout>{children}</PortalLayout>;
}

PortalShell.displayName = 'PortalShell';
