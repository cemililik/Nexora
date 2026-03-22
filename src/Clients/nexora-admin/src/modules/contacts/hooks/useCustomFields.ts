import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  CustomFieldDefinitionDto,
  ContactCustomFieldDto,
  CreateCustomFieldRequest,
  UpdateCustomFieldRequest,
  SetCustomFieldValueRequest,
} from '../types';

export const customFieldKeys = {
  definitions: ['contacts', 'custom-fields'] as const,
  contactFields: (contactId: string) =>
    ['contacts', 'custom-fields', contactId] as const,
};

export function useCustomFieldDefinitions() {
  return useQuery({
    queryKey: customFieldKeys.definitions,
    queryFn: () =>
      api.get<CustomFieldDefinitionDto[]>('/contacts/custom-fields'),
  });
}

export function useCreateCustomField() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateCustomFieldRequest) =>
      api.post<CustomFieldDefinitionDto>('/contacts/custom-fields', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: customFieldKeys.definitions,
      });
      toast.success(t('lockey_contacts_toast_custom_field_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateCustomField(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateCustomFieldRequest) =>
      api.put<CustomFieldDefinitionDto>(
        `/contacts/custom-fields/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: customFieldKeys.definitions,
      });
      toast.success(t('lockey_contacts_toast_custom_field_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteCustomField() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/contacts/custom-fields/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: customFieldKeys.definitions,
      });
      toast.success(t('lockey_contacts_toast_custom_field_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useContactCustomFields(contactId: string) {
  return useQuery({
    queryKey: customFieldKeys.contactFields(contactId),
    queryFn: () =>
      api.get<ContactCustomFieldDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/custom-fields`,
      ),
    enabled: !!contactId,
  });
}

export function useSetCustomFieldValue(contactId: string) {
  const queryClient = useQueryClient();
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: ({
      definitionId,
      data,
    }: {
      definitionId: string;
      data: SetCustomFieldValueRequest;
    }) =>
      api.put<ContactCustomFieldDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/custom-fields/${encodeURIComponent(definitionId)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: customFieldKeys.contactFields(contactId),
      });
    },
    onError: (err) => handleApiError(err),
  });
}
