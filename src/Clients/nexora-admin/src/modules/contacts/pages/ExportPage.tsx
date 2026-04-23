import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Input } from '@/shared/components/ui/input';
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
import { useExportStatus, useStartExport } from '../hooks/useImportExport';
import { ExportFieldPicker } from '../components/ExportFieldPicker';
import type {
  ContactStatus,
  ContactType,
  ExportDateField,
  ExportFormat,
} from '../types';

const ALL_SENTINEL = '__all__';

/**
 * Accepts http(s) absolute URLs and safe relative paths; rejects javascript:, data:,
 * and anything else that could execute script on click. Used before assigning
 * `anchor.href` on the download button.
 */
function isSafeDownloadUrl(url: string): boolean {
  if (url.startsWith('/') && !url.startsWith('//')) return true;
  try {
    const parsed = new URL(url, window.location.origin);
    return parsed.protocol === 'https:' || parsed.protocol === 'http:';
  } catch {
    return false;
  }
}

export default function ExportPage() {
  const { t, i18n } = useTranslation('contacts');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const startExport = useStartExport();

  // Form state (Phase A)
  const [format, setFormat] = useState<ExportFormat>('csv');
  const [fields, setFields] = useState<string[]>([]);
  const [dateField, setDateField] = useState<ExportDateField>('CreatedAt');
  const [dateFrom, setDateFrom] = useState<string>('');
  const [dateTo, setDateTo] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<ContactStatus | undefined>(undefined);
  const [typeFilter, setTypeFilter] = useState<ContactType | undefined>(undefined);

  // Job state (Phase B)
  const [jobId, setJobId] = useState<string | null>(null);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name' },
      { label: 'lockey_contacts_export_title' },
    ]);
  }, [setBreadcrumbs]);

  const hasFormInput =
    fields.length > 0 ||
    !!dateFrom ||
    !!dateTo ||
    !!statusFilter ||
    !!typeFilter ||
    format !== 'csv';

  useUnsavedChangesGuard(hasFormInput && !jobId);

  const statusQuery = useExportStatus(jobId ?? '');

  const handleExport = () => {
    const body = {
      format,
      ...(fields.length > 0 ? { fields } : {}),
      ...(dateFrom ? { dateFrom: new Date(dateFrom).toISOString() } : {}),
      ...(dateTo ? { dateTo: new Date(dateTo).toISOString() } : {}),
      ...(dateField && (dateFrom || dateTo) ? { dateField } : {}),
      ...(statusFilter ? { statusFilter } : {}),
      ...(typeFilter ? { typeFilter } : {}),
    };

    startExport.mutate(body, {
      onSuccess: (data) => setJobId(data.jobId),
      onError: (err) => handleApiError(err),
    });
  };

  const resetForm = () => {
    setFormat('csv');
    setFields([]);
    setDateField('CreatedAt');
    setDateFrom('');
    setDateTo('');
    setStatusFilter(undefined);
    setTypeFilter(undefined);
    setJobId(null);
  };

  // ------- Phase B: Status panel -------
  if (jobId) {
    const job = statusQuery.data;
    const status = job?.status ?? 'Pending';
    return (
      <div className="mx-auto max-w-2xl space-y-6">
        <h1 className="text-2xl font-semibold">{t('lockey_contacts_export_title')}</h1>

        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_contacts_export_status_title')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center gap-2">
              <Badge
                variant={
                  status === 'Completed'
                    ? 'default'
                    : status === 'Failed'
                      ? 'destructive'
                      : 'secondary'
                }
              >
                {t(`lockey_contacts_export_status_${status.toLowerCase()}`)}
              </Badge>
              {job?.format && (
                <span className="text-sm text-muted-foreground">
                  {t('lockey_contacts_export_form_format')}: {job.format.toUpperCase()}
                </span>
              )}
            </div>

            <dl className="space-y-2 text-sm">
              {typeof job?.totalRows === 'number' && (
                <div>
                  <dt className="text-muted-foreground">
                    {t('lockey_contacts_export_status_total_rows')}
                  </dt>
                  <dd>{job.totalRows}</dd>
                </div>
              )}
              {job?.createdAt && (
                <div>
                  <dt className="text-muted-foreground">
                    {t('lockey_contacts_export_status_created_at')}
                  </dt>
                  <dd>{new Date(job.createdAt).toLocaleString(i18n.language)}</dd>
                </div>
              )}
              {job?.completedAt && (
                <div>
                  <dt className="text-muted-foreground">
                    {t('lockey_contacts_export_status_completed_at')}
                  </dt>
                  <dd>{new Date(job.completedAt).toLocaleString(i18n.language)}</dd>
                </div>
              )}
              {status === 'Failed' && job?.errorDetails && (
                <div>
                  <dt className="text-muted-foreground">
                    {t('lockey_contacts_export_status_error_details')}
                  </dt>
                  <dd className="text-destructive">{job.errorDetails}</dd>
                </div>
              )}
            </dl>

            {status === 'Completed' && job?.downloadUrl && (
              <Button
                type="button"
                onClick={() => {
                  // Programmatic anchor click with the `download` attribute. Popup-blockers
                  // suppress window.open in async callbacks (post-poll), but an anchor click
                  // triggered from a direct user gesture is allowed in every major browser.
                  if (!job.downloadUrl) return;
                  if (!isSafeDownloadUrl(job.downloadUrl)) {
                    // Defensive guard — server already issues a presigned URL on its own
                    // bucket, but a stored/poisoned value containing javascript: or other
                    // dangerous schemes must never be navigated to.
                    return;
                  }
                  const anchor = document.createElement('a');
                  anchor.href = job.downloadUrl;
                  anchor.download = '';
                  anchor.rel = 'noopener noreferrer';
                  // Cross-origin presigned URLs can make the browser ignore the
                  // `download` attribute and navigate in the current tab. Forcing a new
                  // tab keeps the admin SPA state (auth, unsaved form, polling) intact.
                  anchor.target = '_blank';
                  document.body.appendChild(anchor);
                  anchor.click();
                  anchor.remove();
                }}
              >
                {t('lockey_contacts_export_button_download')}
              </Button>
            )}

            <div>
              <Button type="button" variant="outline" onClick={resetForm}>
                {t('lockey_contacts_export_button_start_another')}
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  // ------- Phase A: Form -------
  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_contacts_export_title')}</h1>

      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_contacts_export_settings')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          <div>
            <label className="text-sm font-medium">
              {t('lockey_contacts_export_form_format')}
            </label>
            <Select
              value={format}
              onValueChange={(val: string) => setFormat(val as ExportFormat)}
            >
              <SelectTrigger className="mt-1">
                <SelectValue placeholder={t('lockey_contacts_export_form_format')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="csv">
                  {t('lockey_contacts_export_format_csv')}
                </SelectItem>
                <SelectItem value="xlsx">
                  {t('lockey_contacts_export_format_xlsx')}
                </SelectItem>
                <SelectItem value="vcard">
                  {t('lockey_contacts_export_format_vcard')}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>

          <ExportFieldPicker selectedFields={fields} onChange={setFields} />

          <div className="space-y-2">
            <label className="text-sm font-medium">
              {t('lockey_contacts_export_form_date_range')}
            </label>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <div>
                <label className="text-xs text-muted-foreground">
                  {t('lockey_contacts_export_form_date_field')}
                </label>
                <Select
                  value={dateField}
                  onValueChange={(val: string) => setDateField(val as ExportDateField)}
                >
                  <SelectTrigger className="mt-1">
                    <SelectValue
                      placeholder={t('lockey_contacts_export_form_date_field')}
                    />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="CreatedAt">
                      {t('lockey_contacts_export_field_date_created_at')}
                    </SelectItem>
                    <SelectItem value="UpdatedAt">
                      {t('lockey_contacts_export_field_date_updated_at')}
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <label className="text-xs text-muted-foreground">
                  {t('lockey_contacts_export_form_date_from')}
                </label>
                <Input
                  type="date"
                  className="mt-1"
                  value={dateFrom}
                  onChange={(e) => setDateFrom(e.target.value)}
                />
              </div>
              <div>
                <label className="text-xs text-muted-foreground">
                  {t('lockey_contacts_export_form_date_to')}
                </label>
                <Input
                  type="date"
                  className="mt-1"
                  value={dateTo}
                  onChange={(e) => setDateTo(e.target.value)}
                />
              </div>
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">
              {t('lockey_contacts_export_form_filters')}
            </label>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className="text-xs text-muted-foreground">
                  {t('lockey_contacts_export_form_status_filter')}
                </label>
                <Select
                  value={statusFilter ?? ALL_SENTINEL}
                  onValueChange={(val: string) =>
                    setStatusFilter(val === ALL_SENTINEL ? undefined : (val as ContactStatus))
                  }
                >
                  <SelectTrigger className="mt-1">
                    <SelectValue
                      placeholder={t('lockey_contacts_filter_all_statuses')}
                    />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_SENTINEL}>
                      {t('lockey_contacts_filter_all_statuses')}
                    </SelectItem>
                    <SelectItem value="Active">
                      {t('lockey_contacts_status_active')}
                    </SelectItem>
                    <SelectItem value="Archived">
                      {t('lockey_contacts_status_archived')}
                    </SelectItem>
                    <SelectItem value="Merged">
                      {t('lockey_contacts_status_merged')}
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <label className="text-xs text-muted-foreground">
                  {t('lockey_contacts_export_form_type_filter')}
                </label>
                <Select
                  value={typeFilter ?? ALL_SENTINEL}
                  onValueChange={(val: string) =>
                    setTypeFilter(val === ALL_SENTINEL ? undefined : (val as ContactType))
                  }
                >
                  <SelectTrigger className="mt-1">
                    <SelectValue
                      placeholder={t('lockey_contacts_filter_all_types')}
                    />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_SENTINEL}>
                      {t('lockey_contacts_filter_all_types')}
                    </SelectItem>
                    <SelectItem value="Individual">
                      {t('lockey_contacts_type_individual')}
                    </SelectItem>
                    <SelectItem value="Organization">
                      {t('lockey_contacts_type_organization')}
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          <Button
            type="button"
            disabled={startExport.isPending}
            onClick={handleExport}
          >
            {startExport.isPending
              ? t('lockey_common_loading', { ns: 'common' })
              : t('lockey_contacts_export_button_submit')}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
