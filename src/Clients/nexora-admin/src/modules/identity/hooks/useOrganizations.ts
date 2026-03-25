import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import { userKeys } from './useUsers';
import type {
  OrganizationDto,
  OrganizationDetailDto,
  CreateOrganizationRequest,
  UpdateOrganizationRequest,
  OrganizationMemberDto,
  AddMemberRequest,
} from '../types';

export const orgKeys = {
  all: ['identity', 'organizations'] as const,
  list: (params: PaginationParams) =>
    [...orgKeys.all, 'list', params] as const,
  detail: (id: string) => [...orgKeys.all, 'detail', id] as const,
  members: (id: string, params: PaginationParams) =>
    [...orgKeys.all, 'members', id, params] as const,
};

export function useOrganizations(params: PaginationParams) {
  return useQuery({
    queryKey: orgKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<OrganizationDto>>('/identity/organizations', {
        page: params.page,
        pageSize: params.pageSize,
      }),
  });
}

export function useOrganization(id: string) {
  return useQuery({
    queryKey: orgKeys.detail(id),
    queryFn: () =>
      api.get<OrganizationDetailDto>(
        `/identity/organizations/${encodeURIComponent(id)}`,
      ),
    enabled: !!id,
  });
}

export function useCreateOrganization() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (data: CreateOrganizationRequest) =>
      api.post<OrganizationDto>('/identity/organizations', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: orgKeys.all });
      toast.success(t('lockey_identity_org_created'));
    },
  });
}

export function useUpdateOrganization(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (data: UpdateOrganizationRequest) =>
      api.put<OrganizationDto>(
        `/identity/organizations/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: orgKeys.all });
      toast.success(t('lockey_identity_org_updated'));
    },
  });
}

export function useDeleteOrganization() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/identity/organizations/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: orgKeys.all });
      toast.success(t('lockey_identity_org_deleted'));
    },
  });
}

export function useOrganizationMembers(
  orgId: string,
  params: PaginationParams,
) {
  return useQuery({
    queryKey: orgKeys.members(orgId, params),
    queryFn: () =>
      api.get<PagedResult<OrganizationMemberDto>>(
        `/identity/organizations/${encodeURIComponent(orgId)}/members`,
        { page: params.page, pageSize: params.pageSize },
      ),
    enabled: !!orgId,
  });
}

export function useAddMember(orgId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (data: AddMemberRequest) =>
      api.post<OrganizationMemberDto>(
        `/identity/organizations/${encodeURIComponent(orgId)}/members`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: orgKeys.all });
      toast.success(t('lockey_identity_member_added'));
    },
  });
}

export function useAddUserToOrganization(
  userId: string,
  options?: { onSuccess?: () => void; onError?: (err: Error) => void },
) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (orgId: string) =>
      api.post(
        `/identity/organizations/${encodeURIComponent(orgId)}/members`,
        { userId },
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      void queryClient.invalidateQueries({ queryKey: orgKeys.all });
      toast.success(t('lockey_identity_member_added'));
      options?.onSuccess?.();
    },
    onError: options?.onError,
  });
}

export function useRemoveMember(orgId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('identity');

  return useMutation({
    mutationFn: (userId: string) =>
      api.delete(
        `/identity/organizations/${encodeURIComponent(orgId)}/members/${encodeURIComponent(userId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: orgKeys.all });
      toast.success(t('lockey_identity_member_removed'));
    },
  });
}
