import { useQuery } from '@tanstack/react-query';

import { api } from '@/shared/lib/api';
import type { AuditLogDetailDto } from '../types';

export const auditLogDetailKeys = {
  all: ['audit', 'logs'] as const,
  detail: (id: string) => [...auditLogDetailKeys.all, 'detail', id] as const,
};

export function useAuditLogDetail(id: string) {
  return useQuery({
    queryKey: auditLogDetailKeys.detail(id),
    queryFn: () =>
      api.get<AuditLogDetailDto>(`/audit/logs/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}
