import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { DocumentVersionDto, AddVersionRequest } from '../types';
import { documentKeys } from './useDocuments';

export const versionKeys = {
  list: (documentId: string) =>
    [...documentKeys.detail(documentId), 'versions'] as const,
};

export function useDocumentVersions(documentId: string) {
  return useQuery({
    queryKey: versionKeys.list(documentId),
    queryFn: () =>
      api.get<DocumentVersionDto[]>(
        `/documents/documents/${encodeURIComponent(documentId)}/versions`,
      ),
    enabled: !!documentId,
  });
}

export function useAddDocumentVersion(documentId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: AddVersionRequest) =>
      api.post<DocumentVersionDto>(
        `/documents/documents/${encodeURIComponent(documentId)}/versions`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: documentKeys.detail(documentId) });
      void queryClient.invalidateQueries({ queryKey: versionKeys.list(documentId) });
      toast.success(t('lockey_documents_toast_version_added'));
    },
    onError: (err) => handleApiError(err),
  });
}
