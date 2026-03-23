import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { ConsentRecordDto, RecordConsentRequest } from '../types';

export const consentKeys = {
  all: (contactId: string) => ['contacts', 'consents', contactId] as const,
};

export function useConsents(contactId: string) {
  return useQuery({
    queryKey: consentKeys.all(contactId),
    queryFn: () =>
      api.get<ConsentRecordDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/consents`,
      ),
    enabled: !!contactId,
  });
}

export function useRecordConsent(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: RecordConsentRequest) =>
      api.post<ConsentRecordDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/consents`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: consentKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_consent_recorded'));
    },
    onError: (err) => handleApiError(err),
  });
}
