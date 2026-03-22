import { type ReactNode } from 'react';

import { auth } from '@/shared/lib/auth';
import { redirect } from '@/i18n/navigation';
import { PortalShell } from '@/shared/components/layout/PortalShell';

interface PortalRouteLayoutProps {
  children: ReactNode;
  params: Promise<{ locale: string }>;
}

/**
 * Authenticated portal layout — server component.
 * Checks session server-side and redirects unauthenticated users.
 * Client-side auth sync is handled by PortalShell.
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

  return <PortalShell>{children}</PortalShell>;
}
