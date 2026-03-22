import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  ContactRelationshipDto,
  AddRelationshipRequest,
} from '../types';

export const relationshipKeys = {
  all: (contactId: string) =>
    ['contacts', 'relationships', contactId] as const,
};

export function useRelationships(contactId: string) {
  return useQuery({
    queryKey: relationshipKeys.all(contactId),
    queryFn: () =>
      api.get<ContactRelationshipDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/relationships`,
      ),
    enabled: !!contactId,
  });
}

export function useAddRelationship(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: AddRelationshipRequest) =>
      api.post<ContactRelationshipDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/relationships`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: relationshipKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_relationship_added'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useRemoveRelationship(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (relationshipId: string) =>
      api.delete(
        `/contacts/contacts/${encodeURIComponent(contactId)}/relationships/${encodeURIComponent(relationshipId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: relationshipKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_relationship_removed'));
    },
    onError: (err) => handleApiError(err),
  });
}
