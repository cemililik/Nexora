import { type ReactNode } from 'react';

import { auth } from '@/shared/lib/auth';
import { redirect } from '@/i18n/navigation';
import { PortalShell } from '@/shared/components/layout/PortalShell';
import type { UserInfo } from '@/shared/types/auth';

interface PortalRouteLayoutProps {
  children: ReactNode;
  params: Promise<{ locale: string }>;
}

/**
 * Authenticated portal layout — server component.
 * Checks session server-side and redirects unauthenticated users.
 * Fetches user info server-side to eliminate hydration mismatch.
 */
export default async function PortalRouteLayout({
  children,
  params,
}: PortalRouteLayoutProps) {
  const session = await auth();
  const { locale } = await params;

  if (!session || session.error === 'RefreshAccessTokenError') {
    return redirect({ href: '/auth/login', locale });
  }

  // Fetch user info server-side — eliminates hydration mismatch
  // because Zustand is populated before first client render.
  let serverUser: UserInfo | null = null;
  if (session.accessToken) {
    try {
      const apiUrl = process.env.INTERNAL_API_URL ?? 'http://localhost:5100';
      const res = await fetch(`${apiUrl}/api/v1/identity/users/me`, {
        headers: { Authorization: `Bearer ${session.accessToken}` },
        cache: 'no-store',
      });
      if (res.ok) {
        const envelope: { data: UserInfo } = await res.json();
        serverUser = envelope.data;
      }
    } catch {
      // Fallback to client-side fetch via useAuth hook
    }
  }

  return (
    <PortalShell
      serverUser={serverUser}
      serverTenantId={session.tenantId}
      serverOrganizationId={session.organizationId}
      serverPermissions={session.permissions}
      serverAccessToken={session.accessToken}
    >
      {children}
    </PortalShell>
  );
}
