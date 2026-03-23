import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/components/ui/select';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useGenerateImportUploadUrl, useConfirmImport, useImportStatus } from '../hooks/useImportExport';
import type { ExportFormat } from '../types';

export default function ImportPage() {
  const { t, i18n } = useTranslation('contacts');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const generateUploadUrl = useGenerateImportUploadUrl();
  const confirmImport = useConfirmImport();
  const [jobId, setJobId] = useState('');
  const { data: jobStatus } = useImportStatus(jobId);

  const [format, setFormat] = useState<ExportFormat>('csv');
  const [fileName, setFileName] = useState('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name' },
      { label: 'lockey_contacts_import_title' },
    ]);
  }, [setBreadcrumbs]);

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;
      setFileName(file.name);
      setSelectedFile(file);
    },
    [],
  );

  const isPending = generateUploadUrl.isPending || isUploading || confirmImport.isPending;

  const handleImport = async () => {
    if (!selectedFile) return;

    try {
      // Step 1: Request presigned upload URL from backend
      const contentType = format === 'csv'
        ? 'text/csv'
        : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

      const uploadUrlData = await generateUploadUrl.mutateAsync({
        fileName,
        contentType,
        fileSize: selectedFile.size,
      });

      // Step 2: Upload file directly to MinIO via presigned URL
      setIsUploading(true);
      const uploadResponse = await fetch(uploadUrlData.uploadUrl, {
        method: 'PUT',
        body: selectedFile,
        headers: { 'Content-Type': contentType },
      });

      if (!uploadResponse.ok) {
        throw new Error(`Upload failed: ${uploadResponse.status}`);
      }
      setIsUploading(false);

      // Step 3: Confirm upload and start import job
      const jobData = await confirmImport.mutateAsync({
        fileName,
        fileFormat: format,
        storageKey: uploadUrlData.storageKey,
      });

      setJobId(jobData.jobId);
    } catch (err) {
      setIsUploading(false);
      handleApiError(err);
    }
  };

  const progressPercent =
    jobStatus && jobStatus.totalRows > 0
      ? Math.round((jobStatus.processedRows / jobStatus.totalRows) * 100)
      : 0;

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_contacts_import_title')}</h1>

      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_contacts_import_upload')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <label className="text-sm font-medium">{t('lockey_contacts_import_form_file_format')}</label>
            <Select value={format} onValueChange={(val: string) => setFormat(val as ExportFormat)}>
              <SelectTrigger className="mt-1">
                <SelectValue placeholder={t('lockey_contacts_import_form_file_format')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="csv">{t('lockey_contacts_format_csv')}</SelectItem>
                <SelectItem value="xlsx">{t('lockey_contacts_format_xlsx')}</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div>
            <label className="text-sm font-medium">{t('lockey_contacts_import_form_file_name')}</label>
            <input
              ref={fileInputRef}
              type="file"
              accept={format === 'csv' ? '.csv' : '.xlsx'}
              onChange={handleFileChange}
              className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm file:me-4 file:rounded file:border-0 file:bg-primary file:px-4 file:py-1 file:text-sm file:text-primary-foreground"
            />
          </div>

          {fileName && (
            <p className="text-sm text-muted-foreground">
              {t('lockey_contacts_import_selected_file', { fileName })}
            </p>
          )}

          <Button
            type="button"
            disabled={!selectedFile || isPending}
            onClick={handleImport}
          >
            {isPending
              ? t('lockey_common_loading', { ns: 'common' })
              : t('lockey_contacts_import_start')}
          </Button>
        </CardContent>
      </Card>

      {jobStatus && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_contacts_import_status')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center gap-2">
              <Badge
                variant={
                  jobStatus.status === 'Completed'
                    ? 'default'
                    : jobStatus.status === 'Failed'
                      ? 'destructive'
                      : 'secondary'
                }
              >
                {t(`lockey_contacts_import_status_${jobStatus.status.toLowerCase()}`)}
              </Badge>
            </div>

            {/* Progress bar */}
            <div>
              <div className="flex justify-between text-sm mb-1">
                <span>{t('lockey_contacts_import_progress')}</span>
                <span>{progressPercent}%</span>
              </div>
              <div className="h-2 w-full rounded-full bg-muted">
                <div
                  className="h-2 rounded-full bg-primary transition-all"
                  // Inline style required: dynamic progress bar width from computed percentage
                  style={{ width: `${progressPercent}%` }}
                />
              </div>
            </div>

            <dl className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <dt className="text-muted-foreground">{t('lockey_contacts_import_col_total_rows')}</dt>
                <dd className="font-medium">{jobStatus.totalRows}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">{t('lockey_contacts_import_col_processed')}</dt>
                <dd className="font-medium">{jobStatus.processedRows}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">{t('lockey_contacts_import_col_success')}</dt>
                <dd className="font-medium text-green-600">{jobStatus.successCount}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">{t('lockey_contacts_import_col_errors')}</dt>
                <dd className="font-medium text-destructive">{jobStatus.errorCount}</dd>
              </div>
            </dl>

            {jobStatus.completedAt && (
              <p className="text-xs text-muted-foreground">
                {t('lockey_contacts_import_completed_at', {
                  date: new Date(jobStatus.completedAt).toLocaleString(i18n.language),
                })}
              </p>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
