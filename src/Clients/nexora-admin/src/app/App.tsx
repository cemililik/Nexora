import { Suspense } from 'react';
import { RouterProvider } from 'react-router';
import { Toaster } from 'sonner';
import { useTranslation } from 'react-i18next';

import { QueryProvider } from './providers/QueryProvider';
import { AuthProvider } from './providers/AuthProvider';
import { router } from './Router';

/** Suspense fallback that re-renders correctly on language change. */
function LoadingFallback() {
  const { t } = useTranslation('common');

  return (
    <div
      className="flex min-h-screen items-center justify-center"
      role="status"
      aria-label={t('lockey_common_loading')}
    >
      <div className="h-8 w-8 animate-spin rounded-full border-4 border-muted border-t-primary" />
    </div>
  );
}

/** Root application component composing all providers. */
export function App() {
  return (
    <QueryProvider>
      <AuthProvider>
        <Suspense fallback={<LoadingFallback />}>
          <RouterProvider router={router} />
        </Suspense>
        <Toaster position="top-right" richColors />
      </AuthProvider>
    </QueryProvider>
  );
}
