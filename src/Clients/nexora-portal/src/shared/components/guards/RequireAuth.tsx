'use client';

import { useSession } from 'next-auth/react';
import { type ReactNode, useEffect } from 'react';

import { useRouter } from '@/i18n/navigation';

interface RequireAuthProps {
  children: ReactNode;
  fallback?: ReactNode;
}

/**
 * Guard component that redirects unauthenticated users to the login page.
 * Renders a loading state while the session is being resolved.
 */
export function RequireAuth({ children, fallback }: RequireAuthProps) {
  const { status } = useSession();
  const router = useRouter();

  useEffect(() => {
    if (status === 'unauthenticated') {
      router.replace('/auth/login');
    }
  }, [status, router]);

  if (status === 'loading' || status === 'unauthenticated') {
    return (
      fallback ?? (
        <div className="flex min-h-screen items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-muted border-t-accent" />
        </div>
      )
    );
  }

  return <>{children}</>;
}
