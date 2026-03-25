import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult } from '@/shared/types/api';
import type {
  CreateReportDefinitionRequest,
  ReportDefinitionDto,
  TestReportQueryResultDto,
  UpdateReportDefinitionRequest,
} from '../types';

export const reportDefinitionKeys = {
  all: ['reporting', 'definitions'] as const,
  list: (params: Record<string, unknown>) =>
    [...reportDefinitionKeys.all, 'list', params] as const,
  detail: (id: string) => [...reportDefinitionKeys.all, 'detail', id] as const,
};

export function useReportDefinitions(params: {
  page?: number;
  pageSize?: number;
  module?: string;
  category?: string;
  search?: string;
}) {
  return useQuery({
    queryKey: reportDefinitionKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<ReportDefinitionDto>>('/reporting/definitions', params),
  });
}

export function useReportDefinition(id: string) {
  return useQuery({
    queryKey: reportDefinitionKeys.detail(id),
    queryFn: () => api.get<ReportDefinitionDto>(`/reporting/definitions/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useCreateReportDefinition() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateReportDefinitionRequest) =>
      api.post<ReportDefinitionDto>('/reporting/definitions', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: reportDefinitionKeys.all });
      toast.success(t('lockey_reporting_toast_definition_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateReportDefinition() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateReportDefinitionRequest) =>
      api.put<ReportDefinitionDto>(
        `/reporting/definitions/${encodeURIComponent(data.id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: reportDefinitionKeys.all });
      toast.success(t('lockey_reporting_toast_definition_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useTestReportQuery() {
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (queryText: string) =>
      api.post<TestReportQueryResultDto>('/reporting/definitions/test-query', { queryText }),
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteReportDefinition() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/reporting/definitions/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: reportDefinitionKeys.all });
      toast.success(t('lockey_reporting_toast_definition_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}
