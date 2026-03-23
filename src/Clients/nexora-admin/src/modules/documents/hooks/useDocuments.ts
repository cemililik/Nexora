import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  DocumentDto,
  DocumentDetailDto,
  DocumentStatus,
  UploadUrlDto,
  DownloadUrlDto,
  GenerateUploadUrlRequest,
  ConfirmUploadRequest,
  UpdateDocumentMetadataRequest,
  MoveDocumentRequest,
  LinkDocumentRequest,
} from '../types';

export const documentKeys = {
  all: ['documents', 'documents'] as const,
  list: (params: PaginationParams & { folderId?: string; search?: string; status?: DocumentStatus }) =>
    [...documentKeys.all, 'list', params] as const,
  detail: (id: string) => [...documentKeys.all, 'detail', id] as const,
};

export function useDocuments(
  params: PaginationParams & { folderId?: string; search?: string; status?: DocumentStatus },
) {
  return useQuery({
    queryKey: documentKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<DocumentDto>>('/documents/documents', {
        page: params.page,
        pageSize: params.pageSize,
        folderId: params.folderId,
        search: params.search,
        status: params.status,
      }),
  });
}

export function useDocument(id: string) {
  return useQuery({
    queryKey: documentKeys.detail(id),
    queryFn: () =>
      api.get<DocumentDetailDto>(`/documents/documents/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useDocumentDownloadUrl(id: string) {
  return useQuery({
    queryKey: [...documentKeys.detail(id), 'download'] as const,
    queryFn: () =>
      api.get<DownloadUrlDto>(`/documents/documents/${encodeURIComponent(id)}/download`),
    enabled: false, // Only fetch on demand
  });
}

export function useGenerateUploadUrl() {
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: GenerateUploadUrlRequest) =>
      api.post<UploadUrlDto>('/documents/documents/upload-url', data),
    onError: (err) => handleApiError(err),
  });
}

export function useConfirmUpload() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: ConfirmUploadRequest) =>
      api.post<DocumentDto>('/documents/documents/confirm-upload', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success(t('lockey_documents_toast_uploaded'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUpdateDocumentMetadata(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: UpdateDocumentMetadataRequest) =>
      api.put<DocumentDto>(
        `/documents/documents/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success(t('lockey_documents_toast_updated'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useArchiveDocument() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/documents/documents/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success(t('lockey_documents_toast_archived'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useRestoreDocument() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.post<void>(
        `/documents/documents/${encodeURIComponent(id)}/restore`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success(t('lockey_documents_toast_restored'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useMoveDocument(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: MoveDocumentRequest) =>
      api.post<DocumentDto>(
        `/documents/documents/${encodeURIComponent(id)}/move`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success(t('lockey_documents_toast_moved'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useLinkDocument(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: LinkDocumentRequest) =>
      api.post<DocumentDto>(
        `/documents/documents/${encodeURIComponent(id)}/link`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success(t('lockey_documents_toast_linked'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useUnlinkDocument(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: () =>
      api.delete(`/documents/documents/${encodeURIComponent(id)}/link`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.all });
      toast.success(t('lockey_documents_toast_unlinked'));
    },
    onError: (err) => handleApiError(err),
  });
}
