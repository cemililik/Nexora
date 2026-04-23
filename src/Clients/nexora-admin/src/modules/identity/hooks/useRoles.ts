import { useQuery, useMutation, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  RoleDto,
  RoleDetailDto,
  RoleUserDto,
  CreateRoleRequest,
  UpdateRoleRequest,
  PermissionDto,
} from '../types';

export const roleKeys = {
  all: ['identity', 'roles'] as const,
  list: (params?: PaginationParams & { search?: string }) => [...roleKeys.all, 'list', params] as const,
  detail: (id: string) => [...roleKeys.all, 'detail', id] as const,
  users: (id: string, params: PaginationParams) =>
    [...roleKeys.all, 'users', id, params] as const,
  permissions: (module?: string, scope?: 'Platform' | 'Tenant') =>
    ['identity', 'permissions', module ?? 'all', scope ?? 'all'] as const,
};

export interface RoleFilterParams extends PaginationParams {
  search?: string;
}

export function useRoles(): UseQueryResult<RoleDto[], Error>;
export function useRoles(params: RoleFilterParams): UseQueryResult<PagedResult<RoleDto>, Error>;
export function useRoles(params?: RoleFilterParams): UseQueryResult<RoleDto[] | PagedResult<RoleDto>, Error> {
  return useQuery({
    queryKey: roleKeys.list(params),
    queryFn: () =>
      api.get<RoleDto[] | PagedResult<RoleDto>>('/identity/roles', params ? {
        page: params.page,
        pageSize: params.pageSize,
        ...(params.search ? { search: params.search } : {})
      } : undefined),
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

export function usePermissions(module?: string, scope?: 'Platform' | 'Tenant') {
  return useQuery({
    queryKey: roleKeys.permissions(module, scope),
    queryFn: () => {
      const params: Record<string, string> = {};
      if (module) params.module = module;
      if (scope) params.scope = scope;
      return api.get<PermissionDto[]>('/identity/permissions', Object.keys(params).length ? params : undefined);
    },
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

export function useRoleUsers(roleId: string, params: PaginationParams) {
  return useQuery({
    queryKey: roleKeys.users(roleId, params),
    queryFn: () =>
      api.get<PagedResult<RoleUserDto>>(
        `/identity/roles/${encodeURIComponent(roleId)}/users`,
        { page: params.page, pageSize: params.pageSize },
      ),
    enabled: !!roleId,
  });
}

export function useAddUserToRole(roleId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (userId: string) =>
      api.post(
        `/identity/roles/${encodeURIComponent(roleId)}/users`,
        { userId },
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.all });
      toast.success(t('lockey_identity_toast_user_added_to_role'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useRemoveUserFromRole(roleId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (userId: string) =>
      api.delete(
        `/identity/roles/${encodeURIComponent(roleId)}/users/${encodeURIComponent(userId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.all });
      toast.success(t('lockey_identity_toast_user_removed_from_role'));
    },
    onError: (err) => handleApiError(err),
  });
}
