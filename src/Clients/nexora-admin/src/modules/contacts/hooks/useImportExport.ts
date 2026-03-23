import { useQuery, useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

import { api } from '@/shared/lib/api';
import { useApiError } from '@/shared/hooks/useApiError';
import type {
  ImportJobDto,
  ExportJobDto,
  ImportUploadUrlDto,
  GenerateImportUploadUrlRequest,
  ConfirmImportRequest,
  StartExportRequest,
  GdprDeleteRequest,
} from '../types';

export const importKeys = {
  job: (jobId: string) => ['contacts', 'import', jobId] as const,
};

export function useGenerateImportUploadUrl() {
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: GenerateImportUploadUrlRequest) =>
      api.post<ImportUploadUrlDto>('/contacts/contacts/import/upload-url', data),
    onError: (err) => handleApiError(err),
  });
}

export function useConfirmImport() {
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: ConfirmImportRequest) =>
      api.post<ImportJobDto>('/contacts/contacts/import', data),
    onSuccess: () => {
      toast.success(t('lockey_contacts_toast_import_started'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useImportStatus(jobId: string) {
  return useQuery({
    queryKey: importKeys.job(jobId),
    queryFn: () =>
      api.get<ImportJobDto>(
        `/contacts/contacts/import/${encodeURIComponent(jobId)}`,
      ),
    enabled: !!jobId,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status === 'Processing' || status === 'Pending') {
        return 2000;
      }
      return false;
    },
  });
}

export function useStartExport() {
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: StartExportRequest) =>
      api.post<ExportJobDto>('/contacts/contacts/export', data),
    onSuccess: () => {
      toast.success(t('lockey_contacts_toast_export_started'));
    },
    onError: (err) => handleApiError(err),
  });
}

export function useGdprExport(contactId: string) {
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: () =>
      api.post<void>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/gdpr/export`,
      ),
    onError: (err) => handleApiError(err),
  });
}

export function useGdprDelete(contactId: string) {
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: GdprDeleteRequest) =>
      api.post<void>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/gdpr/delete`,
        data,
      ),
    onSuccess: () => {
      toast.warning(t('lockey_contacts_toast_gdpr_delete_requested'));
    },
    onError: (err) => handleApiError(err),
  });
}
