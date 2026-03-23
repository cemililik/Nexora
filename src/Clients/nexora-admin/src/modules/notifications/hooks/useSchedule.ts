import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  NotificationScheduleDto,
  ScheduleNotificationRequest,
} from '../types';
import { notificationKeys } from './useNotifications';

export const scheduleKeys = {
  all: ['notifications', 'schedule'] as const,
  list: (params: PaginationParams) =>
    [...scheduleKeys.all, 'list', params] as const,
};

export function useScheduledNotifications(params: PaginationParams) {
  return useQuery({
    queryKey: scheduleKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<NotificationScheduleDto>>('/notifications/schedule', {
        page: params.page,
        pageSize: params.pageSize,
      }),
  });
}

export function useScheduleNotification() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: ScheduleNotificationRequest) =>
      api.post<NotificationScheduleDto>('/notifications/schedule', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: scheduleKeys.all });
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all });
      toast.success(t('lockey_notifications_schedule_toast_scheduled'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useCancelScheduledNotification() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/notifications/schedule/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: scheduleKeys.all });
      toast.success(t('lockey_notifications_schedule_toast_cancelled'));
    },
    onError: (err) => handleApiError(err),
  });
}
