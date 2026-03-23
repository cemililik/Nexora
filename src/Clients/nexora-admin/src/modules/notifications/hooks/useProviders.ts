import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  NotificationProviderDto,
  NotificationChannel,
  CreateNotificationProviderRequest,
  UpdateNotificationProviderRequest,
  TestNotificationProviderRequest,
} from '../types';

export const providerKeys = {
  all: ['notifications', 'providers'] as const,
  list: (channel?: NotificationChannel) =>
    [...providerKeys.all, 'list', channel] as const,
};

export function useProviders(channel?: NotificationChannel) {
  return useQuery({
    queryKey: providerKeys.list(channel),
    queryFn: () =>
      api.get<NotificationProviderDto[]>('/notifications/providers', {
        channel,
      }),
  });
}

export function useCreateProvider() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateNotificationProviderRequest) =>
      api.post<NotificationProviderDto>('/notifications/providers', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: providerKeys.all });
      toast.success(t('lockey_notifications_providers_toast_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateProvider(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateNotificationProviderRequest) =>
      api.put<NotificationProviderDto>(
        `/notifications/providers/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: providerKeys.all });
      toast.success(t('lockey_notifications_providers_toast_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useTestProvider() {
  const { t } = useTranslation('notifications');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: TestNotificationProviderRequest }) =>
      api.post<void>(
        `/notifications/providers/${encodeURIComponent(id)}/test`,
        data,
      ),
    onSuccess: () => {
      toast.success(t('lockey_notifications_providers_test_toast_success'));
    },
    onError: (err) => handleApiError(err),
  });
}
