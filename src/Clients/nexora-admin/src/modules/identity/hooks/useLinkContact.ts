import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import { userKeys } from './useUsers';

interface LinkContactPayload {
  contactId: string;
}

/**
 * Links an Identity user to a Contacts module contact record.
 * Invalidates user list + detail on success.
 */
export function useLinkContact(userId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: LinkContactPayload) =>
      api.post<void>(
        `/identity/users/${encodeURIComponent(userId)}/link-contact`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success(t('lockey_identity_user_link_contact_success'));
    },
    onError: (err) => handleApiError(err),
  });
}
