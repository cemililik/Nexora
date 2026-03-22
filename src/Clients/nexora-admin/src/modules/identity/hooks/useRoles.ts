import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type { RoleDto, CreateRoleRequest, PermissionDto } from '../types';

export const roleKeys = {
  all: ['identity', 'roles'] as const,
  list: () => [...roleKeys.all, 'list'] as const,
  permissions: (module?: string) =>
    ['identity', 'permissions', module ?? 'all'] as const,
};

export function useRoles() {
  return useQuery({
    queryKey: roleKeys.list(),
    queryFn: () => api.get<RoleDto[]>('/identity/roles'),
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

  return useMutation({
    mutationFn: (data: CreateRoleRequest) =>
      api.post<RoleDto>('/identity/roles', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.all });
      toast.success(t('lockey_identity_role_created'));
    },
  });
}
