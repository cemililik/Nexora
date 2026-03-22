import { Suspense } from 'react';
import { RouterProvider } from 'react-router';
import { Toaster } from 'sonner';

import i18n from '@/shared/lib/i18n';
import { QueryProvider } from './providers/QueryProvider';
import { AuthProvider } from './providers/AuthProvider';
import { router } from './Router';

/** Root application component composing all providers. */
export function App() {
  return (
    <QueryProvider>
      <AuthProvider>
        <Suspense
          fallback={
            <div
              className="flex min-h-screen items-center justify-center"
              role="status"
              aria-label={i18n.t('lockey_common_loading', { ns: 'common' })}
            >
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-muted border-t-primary" />
            </div>
          }
        >
          <RouterProvider router={router} />
        </Suspense>
        <Toaster position="top-right" richColors />
      </AuthProvider>
    </QueryProvider>
  );
}
