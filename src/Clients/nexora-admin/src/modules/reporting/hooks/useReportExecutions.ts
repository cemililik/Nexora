import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult } from '@/shared/types/api';
import type {
  DownloadReportResultDto,
  ExecuteReportRequest,
  ReportExecutionDto,
} from '../types';
import { reportDefinitionKeys } from './useReportDefinitions';

export const reportExecutionKeys = {
  all: ['reporting', 'executions'] as const,
  list: (params: Record<string, unknown>) =>
    [...reportExecutionKeys.all, 'list', params] as const,
  detail: (id: string) => [...reportExecutionKeys.all, 'detail', id] as const,
};

export function useReportExecutions(params: {
  definitionId?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: reportExecutionKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<ReportExecutionDto>>('/reporting/executions', params),
  });
}

export function useReportExecution(id: string) {
  return useQuery({
    queryKey: reportExecutionKeys.detail(id),
    queryFn: () =>
      api.get<ReportExecutionDto>(`/reporting/executions/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useExecuteReport() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('reporting');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: ExecuteReportRequest) =>
      api.post<ReportExecutionDto>('/reporting/executions', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: reportExecutionKeys.all });
      void queryClient.invalidateQueries({ queryKey: reportDefinitionKeys.all });
      toast.success(t('lockey_reporting_toast_execution_queued'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDownloadReportResult(executionId: string) {
  return useQuery({
    queryKey: ['reporting', 'download', executionId] as const,
    queryFn: () =>
      api.get<DownloadReportResultDto>(
        `/reporting/executions/${encodeURIComponent(executionId)}/download`,
      ),
    enabled: false, // Only fetch on demand
  });
}

export function useDownloadReport() {
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (executionId: string) =>
      api.get<DownloadReportResultDto>(
        `/reporting/executions/${encodeURIComponent(executionId)}/download`,
      ),
    onError: (err) => handleApiError(err),
  });
}

export function useReportFile() {
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: async (executionId: string) => {
      const blob = await api.blob(
        `/reporting/executions/${encodeURIComponent(executionId)}/file`,
      );
      return blob;
    },
    onError: (err) => handleApiError(err),
  });
}
