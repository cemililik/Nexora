import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  NotificationTemplateDto,
  NotificationTemplateDetailDto,
  NotificationTemplateTranslationDto,
  NotificationChannel,
  CreateNotificationTemplateRequest,
  UpdateNotificationTemplateRequest,
  AddTemplateTranslationRequest,
} from '../types';

export const templateKeys = {
  all: ['notifications', 'templates'] as const,
  list: (params: PaginationParams & { channel?: NotificationChannel; module?: string; isActive?: boolean }) =>
    [...templateKeys.all, 'list', params] as const,
  detail: (id: string) => [...templateKeys.all, 'detail', id] as const,
};

export function useNotificationTemplates(
  params: PaginationParams & { channel?: NotificationChannel; module?: string; isActive?: boolean },
) {
  return useQuery({
    queryKey: templateKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<NotificationTemplateDto>>('/notifications/templates', {
        page: params.page,
        pageSize: params.pageSize,
        channel: params.channel,
        module: params.module,
        isActive: params.isActive,
      }),
  });
}

export function useNotificationTemplate(id: string) {
  return useQuery({
    queryKey: templateKeys.detail(id),
    queryFn: () =>
      api.get<NotificationTemplateDetailDto>(`/notifications/templates/${encodeURIComponent(id)}`),
    enabled: !!id && id !== 'create',
  });
}

export function useCreateNotificationTemplate() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateNotificationTemplateRequest) =>
      api.post<NotificationTemplateDto>('/notifications/templates', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templateKeys.all });
      toast.success(t('lockey_notifications_templates_toast_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateNotificationTemplate(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateNotificationTemplateRequest) =>
      api.put<NotificationTemplateDto>(
        `/notifications/templates/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templateKeys.all });
      toast.success(t('lockey_notifications_templates_toast_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteNotificationTemplate() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/notifications/templates/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templateKeys.all });
      toast.success(t('lockey_notifications_templates_toast_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useAddTemplateTranslation(templateId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: AddTemplateTranslationRequest) =>
      api.post<NotificationTemplateTranslationDto>(
        `/notifications/templates/${encodeURIComponent(templateId)}/translations`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templateKeys.detail(templateId) });
      toast.success(t('lockey_notifications_translations_toast_added'));
    },
    onError: (err) => handleApiError(err),
  });
}
