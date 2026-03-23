import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { DocumentAccessDto, GrantAccessRequest } from '../types';
import { documentKeys } from './useDocuments';

export const accessKeys = {
  list: (documentId: string) =>
    [...documentKeys.detail(documentId), 'access'] as const,
};

export function useDocumentAccess(documentId: string) {
  return useQuery({
    queryKey: accessKeys.list(documentId),
    queryFn: () =>
      api.get<DocumentAccessDto[]>(
        `/documents/documents/${encodeURIComponent(documentId)}/access`,
      ),
    enabled: !!documentId,
  });
}

export function useGrantDocumentAccess(documentId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: GrantAccessRequest) =>
      api.post<DocumentAccessDto>(
        `/documents/documents/${encodeURIComponent(documentId)}/access`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: accessKeys.list(documentId) });
      toast.success(t('lockey_documents_toast_access_granted'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useRevokeDocumentAccess(documentId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (accessId: string) =>
      api.delete(
        `/documents/documents/${encodeURIComponent(documentId)}/access/${encodeURIComponent(accessId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: accessKeys.list(documentId) });
      toast.success(t('lockey_documents_toast_access_revoked'));
    },
    onError: (err) => handleApiError(err),
  });
}
