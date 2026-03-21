'use client';

import { type ReactNode } from 'react';

import { RequireAuth } from '@/shared/components/guards/RequireAuth';
import { PortalLayout } from '@/shared/components/layout/PortalLayout';
import { useAuth } from '@/shared/hooks/useAuth';

interface PortalRouteLayoutProps {
  children: ReactNode;
}

/**
 * Authenticated portal layout.
 * Wraps all portal pages with auth guard and the main layout shell.
 */
export default function PortalRouteLayout({ children }: PortalRouteLayoutProps) {
  // Initialize auth sync (session → zustand store)
  useAuth();

  return (
    <RequireAuth>
      <PortalLayout>{children}</PortalLayout>
    </RequireAuth>
  );
}
