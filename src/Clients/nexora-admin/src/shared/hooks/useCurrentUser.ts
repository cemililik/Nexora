import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

import { api } from '@/shared/lib/api';
import { useApiError } from './useApiError';

export interface UpdatePreferencesPayload {
  preferredLanguage: string | null;
}

/**
 * Mutation hook to persist the current user's locale preferences.
 * Called when the user changes language in the Topbar — stores the choice
 * server-side so it is restored on next login.
 */
export function useUpdateCurrentUserPreferences() {
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdatePreferencesPayload) =>
      api.patch<void>('/identity/users/me/preferences', data),
    onSuccess: () => {
      toast.success(t('lockey_identity_user_preferences_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}
