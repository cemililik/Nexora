import { useEffect, useCallback } from 'react';
import { useBlocker } from 'react-router';

/**
 * Guards against navigation when unsaved changes exist.
 * Uses React Router v7 `useBlocker` for client-side navigation
 * and `window.onbeforeunload` for browser close/refresh.
 */
export function useUnsavedChangesGuard(isDirty: boolean) {
  const blocker = useBlocker(isDirty);

  const isBlocked = blocker.state === 'blocked';

  const proceed = useCallback(() => {
    if (blocker.state === 'blocked') {
      blocker.proceed();
    }
  }, [blocker]);

  const reset = useCallback(() => {
    if (blocker.state === 'blocked') {
      blocker.reset();
    }
  }, [blocker]);

  useEffect(() => {
    if (!isDirty) return;

    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };

    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [isDirty]);

  return { blocker, isBlocked, proceed, reset };
}
