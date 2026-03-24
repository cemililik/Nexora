import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult } from '@/shared/types/api';
import type { CreateReportScheduleRequest, ReportScheduleDto } from '../types';

export const reportScheduleKeys = {
  all: ['reporting', 'schedules'] as const,
  list: (params: Record<string, unknown>) =>
    [...reportScheduleKeys.all, 'list', params] as const,
};

export function useReportSchedules(params: {
  definitionId?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: reportScheduleKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<ReportScheduleDto>>('/reporting/schedules', params),
  });
}

export function useCreateReportSchedule() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateReportScheduleRequest) =>
      api.post<ReportScheduleDto>('/reporting/schedules', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: reportScheduleKeys.all });
      toast.success(t('lockey_reporting_toast_schedule_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteReportSchedule() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/reporting/schedules/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: reportScheduleKeys.all });
      toast.success(t('lockey_reporting_toast_schedule_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}
