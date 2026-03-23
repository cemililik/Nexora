import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import { documentKeys } from './useDocuments';
import type {
  DocumentTemplateDto,
  DocumentTemplateDetailDto,
  TemplateCategory,
  RenderTemplateResultDto,
  CreateDocumentTemplateRequest,
  UpdateDocumentTemplateRequest,
  RenderTemplateRequest,
} from '../types';

export const templateKeys = {
  all: ['documents', 'templates'] as const,
  list: (params: PaginationParams & { category?: TemplateCategory; isActive?: boolean }) =>
    [...templateKeys.all, 'list', params] as const,
  detail: (id: string) => [...templateKeys.all, 'detail', id] as const,
};

export function useTemplates(
  params: PaginationParams & { category?: TemplateCategory; isActive?: boolean },
) {
  return useQuery({
    queryKey: templateKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<DocumentTemplateDto>>('/documents/templates', {
        page: params.page,
        pageSize: params.pageSize,
        category: params.category,
        isActive: params.isActive,
      }),
  });
}

export function useTemplate(id: string) {
  return useQuery({
    queryKey: templateKeys.detail(id),
    queryFn: () =>
      api.get<DocumentTemplateDetailDto>(`/documents/templates/${encodeURIComponent(id)}`),
    enabled: !!id && id !== 'create',
  });
}

export function useCreateTemplate() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateDocumentTemplateRequest) =>
      api.post<DocumentTemplateDetailDto>('/documents/templates', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templateKeys.all });
      toast.success(t('lockey_documents_templates_toast_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateTemplate(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateDocumentTemplateRequest) =>
      api.put<DocumentTemplateDetailDto>(
        `/documents/templates/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templateKeys.all });
      toast.success(t('lockey_documents_templates_toast_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useActivateTemplate() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.post<void>(`/documents/templates/${encodeURIComponent(id)}/activate`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templateKeys.all });
      toast.success(t('lockey_documents_templates_toast_activated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeactivateTemplate() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.post<void>(`/documents/templates/${encodeURIComponent(id)}/deactivate`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: templateKeys.all });
      toast.success(t('lockey_documents_templates_toast_deactivated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useRenderTemplate(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: RenderTemplateRequest) =>
      api.post<RenderTemplateResultDto>(
        `/documents/templates/${encodeURIComponent(id)}/render`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success(t('lockey_documents_templates_toast_rendered'));
    },
    onError: (err) => handleApiError(err),
  });
}
