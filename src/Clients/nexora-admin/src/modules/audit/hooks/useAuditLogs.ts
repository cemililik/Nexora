import { useQuery } from '@tanstack/react-query';

import { api } from '@/shared/lib/api';
import { DEFAULT_LIST_STALE_TIME } from '@/shared/lib/queryDefaults';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type { AuditLogDto, AuditLogFilters } from '../types';

export const auditLogKeys = {
  all: ['audit', 'logs'] as const,
  list: (params: PaginationParams & AuditLogFilters) =>
    [...auditLogKeys.all, 'list', params] as const,
};

export function useAuditLogs(
  params: PaginationParams & AuditLogFilters,
) {
  return useQuery({
    queryKey: auditLogKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<AuditLogDto>>('/audit/logs', {
        page: params.page,
        pageSize: params.pageSize,
        module: params.module,
        isSuccess: params.isSuccess,
        dateFrom: params.dateFrom,
        dateTo: params.dateTo,
        operation: params.operation,
        userId: params.userId,
        entityType: params.entityType,
      }),
    staleTime: DEFAULT_LIST_STALE_TIME,
  });
}
