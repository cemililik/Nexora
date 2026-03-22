import { Suspense } from 'react';
import { Outlet } from 'react-router';

import { cn } from '@/shared/lib/utils';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useDirection } from '@/shared/hooks/useDirection';
import { ErrorBoundary } from '@/shared/components/feedback/ErrorBoundary';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';

import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';
import { Breadcrumbs } from './Breadcrumbs';

/** Main admin layout: sidebar + topbar + content area. */
export function AppLayout() {
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);
  const dir = useDirection();

  return (
    <div className="min-h-screen bg-background" dir={dir}>
      <Sidebar />
      <Topbar />
      <main
        className={cn(
          'min-h-[calc(100vh-4rem)] p-6 pt-20 transition-all duration-300',
          sidebarOpen
            ? 'ms-[var(--sidebar-width-open)]'
            : 'ms-[var(--sidebar-width-closed)]',
        )}
      >
        <Breadcrumbs />
        <ErrorBoundary>
          <Suspense fallback={<LoadingSkeleton lines={5} />}>
            <Outlet />
          </Suspense>
        </ErrorBoundary>
      </main>
    </div>
  );
}
