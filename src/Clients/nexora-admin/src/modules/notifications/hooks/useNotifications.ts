import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  NotificationDto,
  NotificationDetailDto,
  NotificationChannel,
  NotificationStatus,
  SendNotificationRequest,
  BulkNotificationResultDto,
  SendBulkNotificationRequest,
} from '../types';

export const notificationKeys = {
  all: ['notifications', 'notifications'] as const,
  list: (params: PaginationParams & { channel?: NotificationChannel; status?: NotificationStatus }) =>
    [...notificationKeys.all, 'list', params] as const,
  detail: (id: string) => [...notificationKeys.all, 'detail', id] as const,
};

export function useNotifications(
  params: PaginationParams & { channel?: NotificationChannel; status?: NotificationStatus },
) {
  return useQuery({
    queryKey: notificationKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<NotificationDto>>('/notifications', {
        page: params.page,
        pageSize: params.pageSize,
        channel: params.channel,
        status: params.status,
      }),
  });
}

export function useNotification(id: string) {
  return useQuery({
    queryKey: notificationKeys.detail(id),
    queryFn: () =>
      api.get<NotificationDetailDto>(`/notifications/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useSendNotification() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: SendNotificationRequest) =>
      api.post<NotificationDto>('/notifications/send', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all });
      toast.success(t('lockey_notifications_toast_sent'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useSendBulkNotification() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: SendBulkNotificationRequest) =>
      api.post<BulkNotificationResultDto>('/notifications/bulk', data),
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all });
      toast.success(
        t('lockey_notifications_bulk_toast_sent', {
          queued: result.queuedCount,
          skipped: result.skippedCount,
        }),
      );
    },
    onError: (err) => handleApiError(err),
  });
}
