'use client';

import { type ReactNode } from 'react';

import { useAuth } from '@/shared/hooks/useAuth';

import { PortalLayout } from './PortalLayout';

interface PortalShellProps {
  children: ReactNode;
}

/**
 * Client component wrapper that initializes auth sync (session -> Zustand)
 * and renders the portal layout. Separated from the server layout
 * to preserve SSR benefits for page content passed as children.
 */
export function PortalShell({ children }: PortalShellProps) {
  useAuth();

  return <PortalLayout>{children}</PortalLayout>;
}

PortalShell.displayName = 'PortalShell';
