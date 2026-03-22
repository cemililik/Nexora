import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  TenantDto,
  TenantDetailDto,
  CreateTenantRequest,
  UpdateTenantStatusRequest,
} from '../types';

export const tenantKeys = {
  all: ['identity', 'tenants'] as const,
  list: (params: PaginationParams) =>
    [...tenantKeys.all, 'list', params] as const,
  detail: (id: string) => [...tenantKeys.all, 'detail', id] as const,
};

export function useTenants(params: PaginationParams) {
  return useQuery({
    queryKey: tenantKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<TenantDto>>('/identity/tenants', {
        page: params.page,
        pageSize: params.pageSize,
      }),
  });
}

export function useTenant(id: string) {
  return useQuery({
    queryKey: tenantKeys.detail(id),
    queryFn: () =>
      api.get<TenantDetailDto>(`/identity/tenants/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useCreateTenant() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (data: CreateTenantRequest) =>
      api.post<TenantDto>('/identity/tenants', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tenantKeys.all });
      toast.success(t('lockey_identity_tenant_created'));
    },
  });
}

export function useUpdateTenantStatus(tenantId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  const mutate = useMutation({
    mutationFn: (data: UpdateTenantStatusRequest) =>
      api.put<void>(
        `/identity/tenants/${encodeURIComponent(tenantId)}/status`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: tenantKeys.all });
      toast.success(t('lockey_identity_tenant_status_updated'));
    },
  });

  const activate = useCallback(
    () => mutate.mutate({ action: 'activate' }),
    [mutate],
  );

  const suspend = useCallback(
    () => mutate.mutate({ action: 'suspend' }),
    [mutate],
  );

  const terminate = useCallback(
    () => mutate.mutate({ action: 'terminate' }),
    [mutate],
  );

  return { ...mutate, activate, suspend, terminate };
}
