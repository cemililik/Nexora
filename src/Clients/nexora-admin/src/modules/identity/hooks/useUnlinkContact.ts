import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import { userKeys } from './useUsers';

/**
 * Removes a user↔contact link. Idempotent on the backend.
 * Invalidates user list + detail on success.
 */
export function useUnlinkContact(userId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: () =>
      api.delete<void>(
        `/identity/users/${encodeURIComponent(userId)}/link-contact`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success(t('lockey_identity_user_link_contact_unlink_success'));
    },
    onError: (err) => handleApiError(err),
  });
}
