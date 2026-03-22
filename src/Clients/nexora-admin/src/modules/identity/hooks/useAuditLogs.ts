import { useQuery } from '@tanstack/react-query';

import { api } from '@/shared/lib/api';
import type { PagedResult } from '@/shared/types/api';
import type { AuditLogDto, AuditLogFilterParams } from '../types';

export const auditKeys = {
  all: ['identity', 'audit-logs'] as const,
  list: (params: AuditLogFilterParams) =>
    [...auditKeys.all, 'list', params] as const,
};

export function useAuditLogs(params: AuditLogFilterParams) {
  return useQuery({
    queryKey: auditKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<AuditLogDto>>('/identity/audit-logs', {
        ...(params.userId && { userId: params.userId }),
        ...(params.action && { action: params.action }),
        ...(params.from && { from: params.from }),
        ...(params.to && { to: params.to }),
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      }),
  });
}
