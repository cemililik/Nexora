import { Suspense } from 'react';
import { Outlet } from 'react-router';
import { useTranslation } from 'react-i18next';

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
  const { t } = useTranslation('common');
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);
  const dir = useDirection();

  return (
    <div className="min-h-screen bg-background" dir={dir}>
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:p-4 focus:bg-background focus:text-foreground focus:border focus:rounded-md"
      >
        {t('lockey_common_skip_to_content')}
      </a>
      <Sidebar />
      <Topbar />
      <main
        id="main-content"
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
