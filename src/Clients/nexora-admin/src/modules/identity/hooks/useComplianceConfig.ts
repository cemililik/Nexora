import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  ComplianceKeySummary,
  SetComplianceOverrideBody,
} from '../types/compliance';

export const complianceKeys = {
  all: ['identity', 'compliance'] as const,
  list: () => [...complianceKeys.all, 'list'] as const,
};

export function useComplianceConfig() {
  return useQuery({
    queryKey: complianceKeys.list(),
    queryFn: () =>
      api.get<ComplianceKeySummary[]>('/settings/compliance/'),
  });
}

export function useSetComplianceOverride(key: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (body: SetComplianceOverrideBody) =>
      api.put<ComplianceKeySummary>(
        `/settings/compliance/${encodeURIComponent(key)}`,
        body,
      ),
    onSuccess: async () => {
      // Return the invalidation promise so the mutation settles after the cache refresh
      // and subsequent renders see fresh data; avoids floating-promise lint noise.
      await queryClient.invalidateQueries({ queryKey: complianceKeys.all });
      toast.success(t('lockey_identity_compliance_override_saved'));
    },
    onError: (err) => {
      handleApiError(err);
    },
  });
}

export function useClearComplianceOverride(key: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (reason: string) =>
      // Reason travels in the DELETE body (not the query string) so free-text
      // justification doesn't leak into access logs, referrer headers, or history.
      api.delete<ComplianceKeySummary>(
        `/settings/compliance/${encodeURIComponent(key)}`,
        { reason },
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: complianceKeys.all });
      toast.success(t('lockey_identity_compliance_override_cleared'));
    },
    onError: (err) => {
      handleApiError(err);
    },
  });
}
