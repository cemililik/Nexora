import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  UserDto,
  UserDetailDto,
  CreateUserRequest,
  UpdateProfileRequest,
  UpdateUserStatusRequest,
  AssignRolesRequest,
  RoleDto,
} from '../types';

export interface UserFilterParams extends PaginationParams {
  organizationId?: string;
  roleId?: string;
  search?: string;
}

export const userKeys = {
  all: ['identity', 'users'] as const,
  list: (params: UserFilterParams) =>
    [...userKeys.all, 'list', params] as const,
  detail: (id: string) => [...userKeys.all, 'detail', id] as const,
};

export function useUsers(params: UserFilterParams) {
  return useQuery({
    queryKey: userKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<UserDto>>('/identity/users', {
        page: params.page,
        pageSize: params.pageSize,
        ...(params.organizationId ? { organizationId: params.organizationId } : {}),
        ...(params.roleId ? { roleId: params.roleId } : {}),
        ...(params.search ? { search: params.search } : {}),
      }),
  });
}

export function useUser(id: string) {
  return useQuery({
    queryKey: userKeys.detail(id),
    queryFn: () => api.get<UserDetailDto>(`/identity/users/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useCreateUser() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateUserRequest) =>
      api.post<UserDto>('/identity/users', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success(t('lockey_identity_user_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateProfile(userId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateProfileRequest) =>
      api.put<UserDto>(
        `/identity/users/${encodeURIComponent(userId)}/profile`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success(t('lockey_identity_user_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateUserStatus(userId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  const { handleApiError } = useApiError();

  const mutate = useMutation({
    mutationFn: (data: UpdateUserStatusRequest) =>
      api.put<void>(
        `/identity/users/${encodeURIComponent(userId)}/status`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success(t('lockey_identity_user_status_updated'));
    },
    onError: (err) => handleApiError(err),
  });

  const activate = useCallback(
    () => mutate.mutate({ action: 'activate' }),
    [mutate],
  );

  const deactivate = useCallback(
    () => mutate.mutate({ action: 'deactivate' }),
    [mutate],
  );

  return { ...mutate, activate, deactivate };
}

export function useDeleteUser() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/identity/users/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success(t('lockey_identity_toast_user_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUserRoles(userId: string, organizationId: string) {
  return useQuery({
    queryKey: [...userKeys.detail(userId), 'roles', organizationId] as const,
    queryFn: () =>
      api.get<RoleDto[]>(`/identity/users/${encodeURIComponent(userId)}/roles`, {
        organizationId,
      }),
    enabled: !!userId && !!organizationId,
  });
}

export function useAssignUserRoles(userId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: AssignRolesRequest) =>
      api.put<void>(
        `/identity/users/${encodeURIComponent(userId)}/roles`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success(t('lockey_identity_toast_roles_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}
