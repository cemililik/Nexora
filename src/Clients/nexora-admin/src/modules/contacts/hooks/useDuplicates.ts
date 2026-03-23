import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  DuplicateContactDto,
  MergeResultDto,
  MergeContactsRequest,
} from '../types';
import { contactKeys } from './useContacts';

export const duplicateKeys = {
  all: (contactId: string) =>
    ['contacts', 'duplicates', contactId] as const,
};

export function useDuplicates(contactId: string, threshold?: number) {
  return useQuery({
    queryKey: [...duplicateKeys.all(contactId), threshold] as const,
    queryFn: () =>
      api.get<DuplicateContactDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/duplicates`,
        {
          ...(threshold !== undefined && { threshold }),
        },
      ),
    enabled: !!contactId,
  });
}

export function useMergeContacts() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: MergeContactsRequest) =>
      api.post<MergeResultDto>('/contacts/contacts/merge', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: contactKeys.all });
      toast.success(t('lockey_contacts_contacts_merged'));
    },
    onError: (err) => handleApiError(err),
  });
}
