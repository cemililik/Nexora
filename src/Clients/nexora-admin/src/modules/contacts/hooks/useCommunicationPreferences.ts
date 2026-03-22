import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type {
  CommunicationPreferenceDto,
  UpdatePreferencesRequest,
} from '../types';

export const prefKeys = {
  all: (contactId: string) =>
    ['contacts', 'preferences', contactId] as const,
};

export function useCommunicationPreferences(contactId: string) {
  return useQuery({
    queryKey: prefKeys.all(contactId),
    queryFn: () =>
      api.get<CommunicationPreferenceDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/preferences`,
      ),
    enabled: !!contactId,
  });
}

export function useUpdatePreferences(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: (data: UpdatePreferencesRequest) =>
      api.put<CommunicationPreferenceDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/preferences`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: prefKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_preferences_updated'));
    },
  });
}
