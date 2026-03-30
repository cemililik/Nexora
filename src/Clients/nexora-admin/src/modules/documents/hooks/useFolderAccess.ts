import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type { FolderAccessDto, GrantFolderAccessRequest } from '../types';

export const folderAccessKeys = {
  all: ['documents', 'folder-access'] as const,
  list: (folderId: string) =>
    [...folderAccessKeys.all, 'list', folderId] as const,
};

export function useFolderAccess(folderId: string) {
  return useQuery({
    queryKey: folderAccessKeys.list(folderId),
    queryFn: () =>
      api.get<FolderAccessDto[]>(
        `/documents/folders/${encodeURIComponent(folderId)}/access`,
      ),
    enabled: !!folderId,
  });
}

export function useGrantFolderAccess(folderId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: GrantFolderAccessRequest) =>
      api.post<FolderAccessDto>(
        `/documents/folders/${encodeURIComponent(folderId)}/access`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: folderAccessKeys.list(folderId) });
      toast.success(t('lockey_documents_folder_access_toast_granted'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useRevokeFolderAccess(folderId: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (accessId: string) =>
      api.delete(
        `/documents/folders/${encodeURIComponent(folderId)}/access/${encodeURIComponent(accessId)}`,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: folderAccessKeys.list(folderId) });
      toast.success(t('lockey_documents_folder_access_toast_revoked'));
    },
    onError: (err) => handleApiError(err),
  });
}
