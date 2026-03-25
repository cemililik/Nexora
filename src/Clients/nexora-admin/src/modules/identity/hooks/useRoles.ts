import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  RoleDto,
  RoleDetailDto,
  CreateRoleRequest,
  UpdateRoleRequest,
  PermissionDto,
} from '../types';

export const roleKeys = {
  all: ['identity', 'roles'] as const,
  list: () => [...roleKeys.all, 'list'] as const,
  detail: (id: string) => [...roleKeys.all, 'detail', id] as const,
  permissions: (module?: string) =>
    ['identity', 'permissions', module ?? 'all'] as const,
};

export function useRoles() {
  return useQuery({
    queryKey: roleKeys.list(),
    queryFn: () => api.get<RoleDto[]>('/identity/roles'),
  });
}

export function useRole(id: string) {
  return useQuery({
    queryKey: roleKeys.detail(id),
    queryFn: () =>
      api.get<RoleDetailDto>(`/identity/roles/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function usePermissions(module?: string) {
  return useQuery({
    queryKey: roleKeys.permissions(module),
    queryFn: () =>
      api.get<PermissionDto[]>('/identity/permissions', module ? { module } : undefined),
  });
}

export function useCreateRole() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateRoleRequest) =>
      api.post<RoleDto>('/identity/roles', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.all });
      toast.success(t('lockey_identity_role_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateRole() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: ({ id, ...data }: UpdateRoleRequest & { id: string }) =>
      api.put<RoleDto>(`/identity/roles/${encodeURIComponent(id)}`, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.all });
      toast.success(t('lockey_identity_toast_role_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteRole() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/identity/roles/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.all });
      toast.success(t('lockey_identity_toast_role_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}
