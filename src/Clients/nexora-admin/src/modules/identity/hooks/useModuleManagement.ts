import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { TenantModuleDto } from '@/shared/types/module';

export interface RegisteredModuleDto {
  name: string;
  displayName: string;
  version: string;
  dependencies: string[];
}

export const moduleKeys = {
  all: (tenantId: string) => ['identity', 'modules', tenantId] as const,
  registered: ['identity', 'modules', 'registered'] as const,
};

export function useRegisteredModules() {
  return useQuery({
    queryKey: moduleKeys.registered,
    queryFn: () => api.get<RegisteredModuleDto[]>('/identity/modules/registered'),
    staleTime: 10 * 60 * 1000, // 10 min — module list rarely changes
  });
}

export function useTenantModules(tenantId: string) {
  return useQuery({
    queryKey: moduleKeys.all(tenantId),
    queryFn: () =>
      api.get<TenantModuleDto[]>(
        `/identity/tenants/${encodeURIComponent(tenantId)}/modules`,
      ),
    enabled: !!tenantId,
  });
}

export function useInstallModule(tenantId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (moduleName: string) =>
      api.post<TenantModuleDto>(
        `/identity/tenants/${encodeURIComponent(tenantId)}/modules`,
        { moduleName },
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: moduleKeys.all(tenantId),
      });
      toast.success(t('lockey_identity_module_installed'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useActivateModule(tenantId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (moduleName: string) =>
      api.patch<void>(
        `/identity/tenants/${encodeURIComponent(tenantId)}/modules/${encodeURIComponent(moduleName)}/activate`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: moduleKeys.all(tenantId) });
      toast.success(t('lockey_identity_module_activated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeactivateModule(tenantId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (moduleName: string) =>
      api.patch<void>(
        `/identity/tenants/${encodeURIComponent(tenantId)}/modules/${encodeURIComponent(moduleName)}/deactivate`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: moduleKeys.all(tenantId) });
      toast.success(t('lockey_identity_module_deactivated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUninstallModule(tenantId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (moduleName: string) =>
      api.delete(
        `/identity/tenants/${encodeURIComponent(tenantId)}/modules/${encodeURIComponent(moduleName)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: moduleKeys.all(tenantId),
      });
      toast.success(t('lockey_identity_module_uninstalled'));
    },
    onError: (err) => handleApiError(err),
  });
}
