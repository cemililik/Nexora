import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { UseMutationResult } from '@tanstack/react-query';

interface UseUndoableDeleteOptions {
  deleteMutation: UseMutationResult<unknown, Error, string>;
  restoreMutation: UseMutationResult<unknown, Error, string>;
  getEntityName: (id: string) => string;
  undoDurationMs?: number;
}

/**
 * Wraps a soft-delete mutation with an undo toast.
 * On delete success, shows a toast with an "Undo" action that calls restore.
 */
export function useUndoableDelete({
  deleteMutation,
  restoreMutation,
  getEntityName,
  undoDurationMs = 5000,
}: UseUndoableDeleteOptions) {
  const { t } = useTranslation('common');

  const handleDelete = useCallback(
    (id: string, onSuccess?: () => void) => {
      const name = getEntityName(id);
      deleteMutation.mutate(id, {
        onSuccess: () => {
          onSuccess?.();
          toast(t('lockey_common_deleted_success', { name }), {
            action: {
              label: t('lockey_common_undo'),
              onClick: () => restoreMutation.mutate(id),
            },
            duration: undoDurationMs,
          });
        },
      });
    },
    [deleteMutation, restoreMutation, getEntityName, undoDurationMs, t],
  );

  return { handleDelete, isPending: deleteMutation.isPending };
}
