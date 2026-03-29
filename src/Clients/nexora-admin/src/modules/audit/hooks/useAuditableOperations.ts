import { useQuery } from '@tanstack/react-query';

import { api } from '@/shared/lib/api';
import type { AuditableModuleDto } from '../types';

export const auditableOperationKeys = {
  all: ['audit', 'settings', 'operations'] as const,
};

export function useAuditableOperations() {
  return useQuery({
    queryKey: auditableOperationKeys.all,
    queryFn: () => api.get<AuditableModuleDto[]>('/audit/settings/operations'),
    staleTime: 5 * 60 * 1000, // operations rarely change, cache for 5 minutes
  });
}
