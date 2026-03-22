import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  UserDto,
  UserDetailDto,
  CreateUserRequest,
  UpdateProfileRequest,
  UpdateUserStatusRequest,
} from '../types';

export const userKeys = {
  all: ['identity', 'users'] as const,
  list: (params: PaginationParams) =>
    [...userKeys.all, 'list', params] as const,
  detail: (id: string) => [...userKeys.all, 'detail', id] as const,
};

export function useUsers(params: PaginationParams) {
  return useQuery({
    queryKey: userKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<UserDto>>('/identity/users', {
        page: params.page,
        pageSize: params.pageSize,
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

  return useMutation({
    mutationFn: (data: CreateUserRequest) =>
      api.post<UserDto>('/identity/users', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success(t('lockey_identity_user_created'));
    },
  });
}

export function useUpdateProfile(userId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

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
  });
}

export function useUpdateUserStatus(userId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

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
