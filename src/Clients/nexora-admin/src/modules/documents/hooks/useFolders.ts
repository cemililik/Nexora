import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  FolderDto,
  CreateFolderRequest,
  RenameFolderRequest,
} from '../types';

export const folderKeys = {
  all: ['documents', 'folders'] as const,
  list: (parentFolderId?: string) =>
    [...folderKeys.all, 'list', parentFolderId ?? 'root'] as const,
  detail: (id: string) => [...folderKeys.all, 'detail', id] as const,
};

export function useFolders(parentFolderId?: string) {
  return useQuery({
    queryKey: folderKeys.list(parentFolderId),
    queryFn: () =>
      api.get<FolderDto[]>('/documents/folders', {
        parentFolderId,
      }),
  });
}

export function useFolder(id: string) {
  return useQuery({
    queryKey: folderKeys.detail(id),
    queryFn: () =>
      api.get<FolderDto>(`/documents/folders/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useCreateFolder() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: CreateFolderRequest) =>
      api.post<FolderDto>('/documents/folders', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: folderKeys.all });
      toast.success(t('lockey_documents_folders_toast_created'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useRenameFolder(id: string) {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: RenameFolderRequest) =>
      api.put<FolderDto>(
        `/documents/folders/${encodeURIComponent(id)}`,
        data,
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: folderKeys.all });
      toast.success(t('lockey_documents_folders_toast_renamed'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useDeleteFolder() {
  const queryClient = useQueryClient();
  const { t } = useTranslation('documents');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (id: string) =>
      api.delete(`/documents/folders/${encodeURIComponent(id)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: folderKeys.all });
      toast.success(t('lockey_documents_folders_toast_deleted'));
    },
    onError: (err) => handleApiError(err),
  });
}
