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
  PreviewImportRequest,
  ContactImportPreviewDto,
  ValidateImportRequest,
  ContactImportValidationDto,
  StartExportRequest,
  GdprDeleteRequest,
  GdprExportDto,
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

export function usePreviewImport() {
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: PreviewImportRequest) =>
      api.post<ContactImportPreviewDto>('/contacts/contacts/import/preview', data),
    onError: (err) => handleApiError(err),
  });
}

export function useValidateImport() {
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: (data: ValidateImportRequest) =>
      api.post<ContactImportValidationDto>('/contacts/contacts/import/validate', data),
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
  const { t } = useTranslation('contacts');
  const { handleApiError } = useApiError();

  return useMutation({
    mutationFn: () =>
      api.post<GdprExportDto>(
        `/contacts/contacts/${encodeURIComponent(contactId)}/gdpr/export`,
      ),
    onSuccess: (data) => {
      // Download personal data as JSON file
      const json = JSON.stringify(data, null, 2);
      const blob = new Blob([json], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `gdpr-export-${contactId}-${new Date().toISOString().slice(0, 10)}.json`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
      toast.success(t('lockey_contacts_gdpr_export_completed'));
    },
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
