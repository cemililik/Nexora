import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  ContactAddressDto,
  AddAddressRequest,
  UpdateAddressRequest,
} from '../types';

export const addressKeys = {
  all: (contactId: string) => ['contacts', 'addresses', contactId] as const,
  list: (contactId: string) =>
    [...addressKeys.all(contactId), 'list'] as const,
};

export function useAddresses(contactId: string) {
  return useQuery({
    queryKey: addressKeys.list(contactId),
    queryFn: () =>
      api.get<ContactAddressDto[]>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/addresses`,
      ),
    enabled: !!contactId,
  });
}

export function useAddAddress(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: AddAddressRequest) =>
      api.post<ContactAddressDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/addresses`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: addressKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_address_added'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateAddress(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: ({ addressId, data }: { addressId: string; data: UpdateAddressRequest }) =>
      api.put<ContactAddressDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/addresses/${encodeURIComponent(addressId)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: addressKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_address_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteAddress(contactId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (addressId: string) =>
      api.delete(
        `/contacts/contacts/${encodeURIComponent(contactId)}/addresses/${encodeURIComponent(addressId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: addressKeys.all(contactId),
      });
      toast.success(t('lockey_contacts_toast_address_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}
