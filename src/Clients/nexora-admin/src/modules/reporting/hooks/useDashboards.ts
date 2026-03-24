import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult } from '@/shared/types/api';
import type {
  CreateDashboardRequest,
  DashboardDto,
  UpdateDashboardRequest,
  WidgetDataDto,
} from '../types';

export const dashboardKeys = {
  all: ['reporting', 'dashboards'] as const,
  list: (params: Record<string, unknown>) =>
    [...dashboardKeys.all, 'list', params] as const,
  detail: (id: string) => [...dashboardKeys.all, 'detail', id] as const,
  widgetData: (dashboardId: string, widgetId: string) =>
    [...dashboardKeys.all, 'widget', dashboardId, widgetId] as const,
};

export function useDashboards(params: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: dashboardKeys.list(params),
    queryFn: () => api.get<PagedResult<DashboardDto>>('/reporting/dashboards', params),
  });
}

export function useDashboard(id: string) {
  return useQuery({
    queryKey: dashboardKeys.detail(id),
    queryFn: () => api.get<DashboardDto>(`/reporting/dashboards/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useCreateDashboard() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateDashboardRequest) =>
      api.post<DashboardDto>('/reporting/dashboards', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: dashboardKeys.all });
      toast.success(t('lockey_reporting_toast_dashboard_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateDashboard() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateDashboardRequest) =>
      api.put<DashboardDto>(
        `/reporting/dashboards/${encodeURIComponent(data.id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: dashboardKeys.all });
      toast.success(t('lockey_reporting_toast_dashboard_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteDashboard() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/reporting/dashboards/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: dashboardKeys.all });
      toast.success(t('lockey_reporting_toast_dashboard_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useWidgetData(dashboardId: string, widgetId: string) {
  return useQuery({
    queryKey: dashboardKeys.widgetData(dashboardId, widgetId),
    queryFn: () =>
      api.get<WidgetDataDto>(
        `/reporting/dashboards/${encodeURIComponent(dashboardId)}/widgets/${encodeURIComponent(widgetId)}/data`,
      ),
    enabled: !!dashboardId && !!widgetId,
  });
}
