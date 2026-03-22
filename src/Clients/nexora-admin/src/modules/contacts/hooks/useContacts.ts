import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  ContactDto,
  ContactDetailDto,
  Contact360Dto,
  ContactStatus,
  ContactType,
  CreateContactRequest,
  UpdateContactRequest,
} from '../types';

export const contactKeys = {
  all: ['contacts', 'contacts'] as const,
  list: (params: PaginationParams & { search?: string; status?: ContactStatus; type?: ContactType }) =>
    [...contactKeys.all, 'list', params] as const,
  detail: (id: string) => [...contactKeys.all, 'detail', id] as const,
  view360: (id: string) => [...contactKeys.all, '360', id] as const,
};

export function useContacts(
  params: PaginationParams & { search?: string; status?: ContactStatus; type?: ContactType },
) {
  return useQuery({
    queryKey: contactKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<ContactDto>>('/contacts/contacts', {
        page: params.page,
        pageSize: params.pageSize,
        search: params.search,
        status: params.status,
        type: params.type,
      }),
  });
}

export function useContact(id: string) {
  return useQuery({
    queryKey: contactKeys.detail(id),
    queryFn: () =>
      api.get<ContactDetailDto>(`/contacts/contacts/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useContact360(id: string) {
  return useQuery({
    queryKey: contactKeys.view360(id),
    queryFn: () =>
      api.get<Contact360Dto>(`/contacts/contacts/${encodeURIComponent(id)}/360`),
    enabled: !!id,
  });
}

export function useCreateContact() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: (data: CreateContactRequest) =>
      api.post<ContactDto>('/contacts/contacts', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: contactKeys.all });
      toast.success(t('lockey_contacts_toast_created'));
    },
  });
}

export function useUpdateContact(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: (data: UpdateContactRequest) =>
      api.put<ContactDto>(
        `/contacts/contacts/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: contactKeys.all });
      toast.success(t('lockey_contacts_toast_updated'));
    },
  });
}

export function useArchiveContact() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/contacts/contacts/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: contactKeys.all });
      toast.success(t('lockey_contacts_toast_archived'));
    },
  });
}

export function useRestoreContact() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');

  return useMutation({
    mutationFn: (id: string) =>
      api.post<void>(
        `/contacts/contacts/${encodeURIComponent(id)}/restore`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: contactKeys.all });
      toast.success(t('lockey_contacts_toast_restored'));
    },
  });
}
