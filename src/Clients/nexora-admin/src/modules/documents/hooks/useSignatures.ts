import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { PagedResult, PaginationParams } from '@/shared/types/api';
import type {
  SignatureRequestDto,
  SignatureRequestDetailDto,
  SignatureRequestStatus,
  CreateSignatureRequestRequest,
} from '../types';

export const signatureKeys = {
  all: ['documents', 'signatures'] as const,
  list: (params: PaginationParams & { documentId?: string; status?: SignatureRequestStatus }) =>
    [...signatureKeys.all, 'list', params] as const,
  detail: (id: string) => [...signatureKeys.all, 'detail', id] as const,
};

export function useSignatures(
  params: PaginationParams & { documentId?: string; status?: SignatureRequestStatus },
) {
  return useQuery({
    queryKey: signatureKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<SignatureRequestDto>>('/documents/signatures', {
        page: params.page,
        pageSize: params.pageSize,
        documentId: params.documentId,
        status: params.status,
      }),
  });
}

export function useSignature(id: string) {
  return useQuery({
    queryKey: signatureKeys.detail(id),
    queryFn: () =>
      api.get<SignatureRequestDetailDto>(`/documents/signatures/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useCreateSignatureRequest() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateSignatureRequestRequest) =>
      api.post<SignatureRequestDetailDto>('/documents/signatures', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: signatureKeys.all });
      toast.success(t('lockey_documents_signatures_toast_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useSendSignatureRequest() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.post<void>(`/documents/signatures/${encodeURIComponent(id)}/send`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: signatureKeys.all });
      toast.success(t('lockey_documents_signatures_toast_sent'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useCancelSignatureRequest() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/documents/signatures/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: signatureKeys.all });
      toast.success(t('lockey_documents_signatures_toast_cancelled'));
    },
    onError: (err) => handleApiError(err),
  });
}
