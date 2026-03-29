import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { AuditSettingDto } from '../types';

export const auditSettingKeys = {
  all: ['audit', 'settings'] as const,
};

export function useAuditSettings() {
  return useQuery({
    queryKey: auditSettingKeys.all,
    queryFn: () => api.get<AuditSettingDto[]>('/audit/settings'),
  });
}

export interface BulkAuditSettingItem {
  module: string;
  operation: string;
  isEnabled: boolean;
  retentionDays: number;
}

export function useUpdateAuditSetting() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('audit');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: AuditSettingDto) =>
      api.put<AuditSettingDto>('/audit/settings', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: auditSettingKeys.all });
      toast.success(t('lockey_audit_settings_saved'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useBulkUpdateAuditSettings() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('audit');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (settings: BulkAuditSettingItem[]) =>
      api.put<AuditSettingDto[]>('/audit/settings/bulk', { settings }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: auditSettingKeys.all });
      toast.success(t('lockey_audit_settings_saved'));
    },
    onError: (err) => handleApiError(err),
  });
}
