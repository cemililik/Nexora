import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type { TenantModuleDto } from '@/shared/types/module';

export const moduleKeys = {
  all: (tenantId: string) => ['identity', 'modules', tenantId] as const,
};

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
  });
}

export function useActivateModule(tenantId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (moduleName: string) =>
      api.patch<void>(
        `/identity/tenants/${encodeURIComponent(tenantId)}/modules/${encodeURIComponent(moduleName)}/activate`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: moduleKeys.all(tenantId) });
      toast.success(t('lockey_identity_module_activated'));
    },
  });
}

export function useDeactivateModule(tenantId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (moduleName: string) =>
      api.patch<void>(
        `/identity/tenants/${encodeURIComponent(tenantId)}/modules/${encodeURIComponent(moduleName)}/deactivate`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: moduleKeys.all(tenantId) });
      toast.success(t('lockey_identity_module_deactivated'));
    },
  });
}

export function useUninstallModule(tenantId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

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
  });
}
