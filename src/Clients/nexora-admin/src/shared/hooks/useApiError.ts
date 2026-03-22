import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

import { extractApiError } from '@/shared/lib/api';

/**
 * Hook for handling API errors uniformly.
 * Maps validation errors to form fields or shows toast for general errors.
 */
export function useApiError<T extends FieldValues>() {
  const { t } = useTranslation();

  const handleApiError = useCallback(
    (error: unknown, setError?: UseFormSetError<T>) => {
      const { message, meta, errors } = extractApiError(error);

      if (errors?.length && setError) {
        for (const e of errors) {
          setError(e.key as Path<T>, {
            type: 'server',
            message: t(e.key, e.params ?? {}),
          });
        }
      } else {
        toast.error(t(message, meta ?? {}));
      }
    },
    [t],
  );

  return { handleApiError };
}
