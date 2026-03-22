import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type { ContactActivityDto, LogActivityRequest } from '../types';

export const activityKeys = {
  all: (contactId: string) =>
    ['contacts', 'activities', contactId] as const,
};

export function useActivities(contactId: string) {
  return useQuery({
    queryKey: activityKeys.all(contactId),
    queryFn: () =>
      api.get<ContactActivityDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/activities`,
      ),
    enabled: !!contactId,
  });
}

export function useLogActivity(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: (data: LogActivityRequest) =>
      api.post<ContactActivityDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/activities`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: activityKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_activity_logged'));
    },
  });
}
