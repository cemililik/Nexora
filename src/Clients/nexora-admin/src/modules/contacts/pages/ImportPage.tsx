import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';

import {
  ImportColumnMapping,
  SKIP_MAPPING,
} from '../components/ImportColumnMapping';
import {
  useConfirmImport,
  useGenerateImportUploadUrl,
  useImportStatus,
  usePreviewImport,
  useValidateImport,
} from '../hooks/useImportExport';
import type {
  ContactImportPreviewDto,
  ContactImportValidationDto,
  ExportFormat,
} from '../types';

type WizardStep =
  | 'upload'
  | 'mapping'
  | 'validate'
  | 'submitting'
  | 'complete';

/** Build a reasonable default mapping: if the header matches a target field
 *  by case-insensitive equality, map it; otherwise mark as skip. */
function buildIdentityMapping(headers: string[]): Record<string, string> {
  const targets = [
    'firstName',
    'lastName',
    'email',
    'phone',
    'mobile',
    'website',
    'companyName',
    'taxId',
    'title',
  ];
  const mapping: Record<string, string> = {};
  for (const header of headers) {
    const match = targets.find(
      (t) => t.toLowerCase() === header.toLowerCase().replace(/[\s_-]/g, ''),
    );
    mapping[header] = match ?? SKIP_MAPPING;
  }
  return mapping;
}

/** Filter out `__skip__` entries before sending the mapping to the backend. */
function toSubmissionMapping(
  mapping: Record<string, string>,
): Record<string, string> {
  const submission: Record<string, string> = {};
  for (const [source, target] of Object.entries(mapping)) {
    if (target && target !== SKIP_MAPPING) submission[source] = target;
  }
  return submission;
}

export default function ImportPage() {
  const { t, i18n } = useTranslation('contacts');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const generateUploadUrl = useGenerateImportUploadUrl();
  const previewImport = usePreviewImport();
  const validateImport = useValidateImport();
  const confirmImport = useConfirmImport();

  const [step, setStep] = useState<WizardStep>('upload');
  const [format, setFormat] = useState<ExportFormat>('csv');
  const [fileName, setFileName] = useState('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [storageKey, setStorageKey] = useState('');
  const [isUploading, setIsUploading] = useState(false);
  const [preview, setPreview] = useState<ContactImportPreviewDto | null>(null);
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [validation, setValidation] =
    useState<ContactImportValidationDto | null>(null);
  const [jobId, setJobId] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { data: jobStatus } = useImportStatus(jobId);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name' },
      { label: 'lockey_contacts_import_title' },
    ]);
  }, [setBreadcrumbs]);

  // Guard against accidental navigation mid-wizard.
  const isDirty = step !== 'upload' && step !== 'complete';
  useUnsavedChangesGuard(isDirty);

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;
      setFileName(file.name);
      setSelectedFile(file);
    },
    [],
  );

  const resetWizard = useCallback(() => {
    setStep('upload');
    setSelectedFile(null);
    setFileName('');
    setStorageKey('');
    setPreview(null);
    setMapping({});
    setValidation(null);
    setJobId('');
    if (fileInputRef.current) fileInputRef.current.value = '';
  }, []);

  const handleUploadAndPreview = async () => {
    if (!selectedFile) return;

    try {
      const contentType =
        format === 'csv'
          ? 'text/csv'
          : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

      const uploadUrlData = await generateUploadUrl.mutateAsync({
        fileName,
        contentType,
        fileSize: selectedFile.size,
      });

      setIsUploading(true);
      const uploadResponse = await fetch(uploadUrlData.uploadUrl, {
        method: 'PUT',
        body: selectedFile,
        headers: { 'Content-Type': contentType },
      });
      setIsUploading(false);

      if (!uploadResponse.ok) {
        throw new Error(
          t('lockey_contacts_import_error_upload_failed', {
            status: uploadResponse.status,
          }),
        );
      }

      setStorageKey(uploadUrlData.storageKey);

      const previewData = await previewImport.mutateAsync({
        storageKey: uploadUrlData.storageKey,
        fileFormat: format,
      });

      setPreview(previewData);
      setMapping(buildIdentityMapping(previewData.headers));
      setStep('mapping');
    } catch (err) {
      setIsUploading(false);
      handleApiError(err);
    }
  };

  const handleValidate = async () => {
    if (!storageKey) return;
    try {
      const result = await validateImport.mutateAsync({
        storageKey,
        fileFormat: format,
        columnMapping: toSubmissionMapping(mapping),
      });
      setValidation(result);
      setStep('validate');
    } catch (err) {
      handleApiError(err);
    }
  };

  const handleStartImport = async () => {
    if (!storageKey) return;
    setStep('submitting');
    try {
      const jobData = await confirmImport.mutateAsync({
        fileName,
        fileFormat: format,
        storageKey,
        columnMapping: toSubmissionMapping(mapping),
      });
      setJobId(jobData.jobId);
      setStep('complete');
    } catch (err) {
      setStep('validate');
      handleApiError(err);
    }
  };

  const progressPercent =
    jobStatus && jobStatus.totalRows > 0
      ? Math.round((jobStatus.processedRows / jobStatus.totalRows) * 100)
      : 0;

  const isUploadPending =
    generateUploadUrl.isPending || isUploading || previewImport.isPending;

  const stepIndex = useMemo(() => {
    switch (step) {
      case 'upload':
        return 0;
      case 'mapping':
        return 1;
      case 'validate':
        return 2;
      case 'submitting':
        return 3;
      case 'complete':
        return 4;
    }
  }, [step]);

  const stepLabels: { key: WizardStep; label: string }[] = [
    { key: 'upload', label: 'lockey_contacts_import_step_upload' },
    { key: 'mapping', label: 'lockey_contacts_import_step_mapping' },
    { key: 'validate', label: 'lockey_contacts_import_step_validate' },
    { key: 'submitting', label: 'lockey_contacts_import_step_submitting' },
    { key: 'complete', label: 'lockey_contacts_import_step_complete' },
  ];

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <h1 className="text-2xl font-semibold">
        {t('lockey_contacts_import_title')}
      </h1>

      {/* Step indicator */}
      <ol className="flex items-center gap-3 text-xs">
        {stepLabels.map((s, i) => (
          <li
            key={s.key}
            className={
              i === stepIndex
                ? 'font-semibold text-primary'
                : i < stepIndex
                  ? 'text-muted-foreground line-through'
                  : 'text-muted-foreground'
            }
          >
            {i + 1}. {t(s.label)}
          </li>
        ))}
      </ol>

      {/* Upload step */}
      {step === 'upload' && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_contacts_import_step_upload')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <label className="text-sm font-medium">
                {t('lockey_contacts_import_form_file_format')}
              </label>
              <Select
                value={format}
                onValueChange={(val: string) => {
                  setFormat(val as ExportFormat);
                  // Reset any previously selected file — its content-type may
                  // no longer match the newly chosen format.
                  setSelectedFile(null);
                  setFileName('');
                  if (fileInputRef.current) fileInputRef.current.value = '';
                }}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue
                    placeholder={t('lockey_contacts_import_form_file_format')}
                  />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="csv">
                    {t('lockey_contacts_format_csv')}
                  </SelectItem>
                  <SelectItem value="xlsx">
                    {t('lockey_contacts_format_xlsx')}
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div>
              <label className="text-sm font-medium">
                {t('lockey_contacts_import_form_file_name')}
              </label>
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
              disabled={!selectedFile || isUploadPending}
              onClick={handleUploadAndPreview}
            >
              {isUploadPending
                ? t('lockey_common_loading', { ns: 'common' })
                : t('lockey_contacts_import_wizard_upload_and_preview')}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Mapping step */}
      {step === 'mapping' && preview && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_contacts_import_mapping_title')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">
              {t('lockey_contacts_import_validation_total_rows', {
                count: preview.totalRowCount,
              })}
            </p>

            <ImportColumnMapping
              headers={preview.headers}
              mapping={mapping}
              onChange={setMapping}
            />

            {/* Preview rows (first 5) */}
            {preview.rows.length > 0 && (
              <div>
                <h3 className="mb-2 text-sm font-medium">
                  {t('lockey_contacts_import_preview_sample')}
                </h3>
                <div className="overflow-x-auto rounded border">
                  <table className="min-w-full text-xs">
                    <thead>
                      <tr className="bg-muted/50">
                        {preview.headers.map((h) => (
                          <th key={h} className="px-2 py-1 text-left font-mono">
                            {h}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {preview.rows.map((row, rowIdx) => (
                        <tr key={rowIdx} className="border-t">
                          {preview.headers.map((h) => (
                            <td key={h} className="px-2 py-1">
                              {row[h] ?? ''}
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={resetWizard}
                disabled={validateImport.isPending}
              >
                {t('lockey_contacts_import_wizard_back')}
              </Button>
              <Button
                type="button"
                onClick={handleValidate}
                disabled={validateImport.isPending}
              >
                {validateImport.isPending
                  ? t('lockey_common_loading', { ns: 'common' })
                  : t('lockey_contacts_import_wizard_next')}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Validate step */}
      {step === 'validate' && validation && (
        <Card>
          <CardHeader>
            <CardTitle>
              {t('lockey_contacts_import_validation_title')}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <dl className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-muted-foreground">
                  {t('lockey_contacts_import_validation_total_rows', {
                    count: validation.totalRows,
                  })}
                </dt>
                <dd className="font-medium">{validation.totalRows}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">
                  {t('lockey_contacts_import_validation_error_count')}
                </dt>
                <dd
                  className={
                    validation.errorCount > 0
                      ? 'font-medium text-destructive'
                      : 'font-medium text-green-600'
                  }
                >
                  {validation.errorCount}
                </dd>
              </div>
            </dl>

            {validation.errorCount === 0 ? (
              <p className="rounded border border-green-500/40 bg-green-500/10 p-3 text-sm text-green-700">
                {t('lockey_contacts_import_validation_no_errors')}
              </p>
            ) : (
              <div className="rounded border">
                <table className="min-w-full text-xs">
                  <thead>
                    <tr className="bg-muted/50">
                      <th className="px-2 py-1 text-left">
                        {t('lockey_contacts_import_validation_col_row')}
                      </th>
                      <th className="px-2 py-1 text-left">
                        {t('lockey_contacts_import_validation_col_field')}
                      </th>
                      <th className="px-2 py-1 text-left">
                        {t('lockey_contacts_import_validation_col_error')}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {validation.errors.map((err, idx) => (
                      <tr key={idx} className="border-t">
                        <td className="px-2 py-1">{err.rowNumber}</td>
                        <td className="px-2 py-1">
                          {err.fieldName ??
                            t('lockey_contacts_import_validation_field_none')}
                        </td>
                        <td className="px-2 py-1">{t(err.errorKey)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => setStep('mapping')}
                disabled={confirmImport.isPending}
              >
                {t('lockey_contacts_import_validation_back_to_mapping')}
              </Button>
              <Button
                type="button"
                onClick={handleStartImport}
                disabled={confirmImport.isPending}
                variant={validation.errorCount > 0 ? 'destructive' : 'default'}
              >
                {confirmImport.isPending
                  ? t('lockey_common_loading', { ns: 'common' })
                  : validation.errorCount > 0
                    ? t('lockey_contacts_import_validation_proceed_anyway')
                    : t('lockey_contacts_import_wizard_start_import')}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Submitting + Complete: show job status */}
      {(step === 'submitting' || step === 'complete') && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_contacts_import_status')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {jobStatus && (
              <>
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
                    {t(
                      `lockey_contacts_import_status_${jobStatus.status.toLowerCase()}`,
                    )}
                  </Badge>
                </div>

                <div>
                  <div className="mb-1 flex justify-between text-sm">
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

                <dl className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
                  <div>
                    <dt className="text-muted-foreground">
                      {t('lockey_contacts_import_col_total_rows')}
                    </dt>
                    <dd className="font-medium">{jobStatus.totalRows}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">
                      {t('lockey_contacts_import_col_processed')}
                    </dt>
                    <dd className="font-medium">{jobStatus.processedRows}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">
                      {t('lockey_contacts_import_col_success')}
                    </dt>
                    <dd className="font-medium text-green-600">
                      {jobStatus.successCount}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">
                      {t('lockey_contacts_import_col_errors')}
                    </dt>
                    <dd className="font-medium text-destructive">
                      {jobStatus.errorCount}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">
                      {t('lockey_contacts_import_col_skipped')}
                    </dt>
                    <dd className="font-medium text-muted-foreground">
                      {jobStatus.skippedCount}
                    </dd>
                  </div>
                </dl>

                {jobStatus.completedAt && (
                  <p className="text-xs text-muted-foreground">
                    {t('lockey_contacts_import_completed_at', {
                      date: new Date(jobStatus.completedAt).toLocaleString(
                        i18n.language,
                      ),
                    })}
                  </p>
                )}
              </>
            )}

            {step === 'complete' && (
              <Button type="button" variant="outline" onClick={resetWizard}>
                {t('lockey_contacts_import_wizard_import_another')}
              </Button>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
