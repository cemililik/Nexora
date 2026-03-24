import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  TagDto,
  TagCategory,
  CreateTagRequest,
  UpdateTagRequest,
} from '../types';
import { contactKeys } from './useContacts';

export const tagKeys = {
  all: ['contacts', 'tags'] as const,
  list: (params?: { category?: TagCategory }) =>
    [...tagKeys.all, 'list', params] as const,
};

export function useTags(params?: { category?: TagCategory }) {
  return useQuery({
    queryKey: tagKeys.list(params),
    queryFn: () =>
      api.get<TagDto[]>('/contacts/tags', {
        category: params?.category,
      }),
  });
}

export function useCreateTag() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateTagRequest) =>
      api.post<TagDto>('/contacts/tags', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tagKeys.all });
      toast.success(t('lockey_contacts_toast_tag_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateTag(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateTagRequest) =>
      api.put<TagDto>(
        `/contacts/tags/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tagKeys.all });
      toast.success(t('lockey_contacts_toast_tag_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteTag() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/contacts/tags/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tagKeys.all });
      toast.success(t('lockey_contacts_toast_tag_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useAssignTag() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: ({ contactId, tagId }: { contactId: string; tagId: string }) =>
      api.post<void>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/tags/${encodeURIComponent(tagId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: contactKeys.all });
      toast.success(t('lockey_contacts_toast_tag_assigned'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useRemoveTag() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: ({ contactId, tagId }: { contactId: string; tagId: string }) =>
      api.delete(
        `/contacts/contacts/${encodeURIComponent(contactId)}/tags/${encodeURIComponent(tagId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: contactKeys.all });
      toast.success(t('lockey_contacts_toast_tag_removed'));
    },
    onError: (err) => handleApiError(err),
  });
}
