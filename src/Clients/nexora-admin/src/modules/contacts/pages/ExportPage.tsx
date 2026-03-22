import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useStartExport } from '../hooks/useImportExport';
import type { ExportFormat, ContactStatus, ContactType } from '../types';

export default function ExportPage() {
  const { t, i18n } = useTranslation('contacts');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const startExport = useStartExport();

  const [format, setFormat] = useState<ExportFormat>('csv');
  const [statusFilter, setStatusFilter] = useState<ContactStatus | undefined>(undefined);
  const [typeFilter, setTypeFilter] = useState<ContactType | undefined>(undefined);
  const [exportResult, setExportResult] = useState<{
    jobId: string;
    status: string;
    format: ExportFormat;
    createdAt: string;
    completedAt?: string;
    downloadUrl?: string;
  } | null>(null);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_contacts_module_name' },
      { label: 'lockey_contacts_export_title' },
    ]);
  }, [setBreadcrumbs]);

  const handleExport = () => {
    startExport.mutate(
      {
        format,
        statusFilter,
        typeFilter,
      },
      {
        onSuccess: (data) => {
          setExportResult(data);
        },
        onError: (err) => handleApiError(err),
      },
    );
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_contacts_export_title')}</h1>

      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_contacts_export_settings')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <label className="text-sm font-medium">{t('lockey_contacts_export_form_format')}</label>
            <select
              value={format}
              onChange={(e) => setFormat(e.target.value as ExportFormat)}
              className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            >
              <option value="csv">{t('lockey_contacts_format_csv')}</option>
              <option value="xlsx">{t('lockey_contacts_format_xlsx')}</option>
            </select>
          </div>

          <div>
            <label className="text-sm font-medium">{t('lockey_contacts_export_form_status_filter')}</label>
            <select
              value={statusFilter ?? ''}
              onChange={(e) =>
                setStatusFilter(e.target.value ? (e.target.value as ContactStatus) : undefined)
              }
              className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            >
              <option value="">{t('lockey_contacts_filter_all_statuses')}</option>
              <option value="Active">{t('lockey_contacts_status_active')}</option>
              <option value="Archived">{t('lockey_contacts_status_archived')}</option>
            </select>
          </div>

          <div>
            <label className="text-sm font-medium">{t('lockey_contacts_export_form_type_filter')}</label>
            <select
              value={typeFilter ?? ''}
              onChange={(e) =>
                setTypeFilter(e.target.value ? (e.target.value as ContactType) : undefined)
              }
              className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            >
              <option value="">{t('lockey_contacts_filter_all_types')}</option>
              <option value="Individual">{t('lockey_contacts_type_individual')}</option>
              <option value="Organization">{t('lockey_contacts_type_organization')}</option>
            </select>
          </div>

          <Button
            type="button"
            disabled={startExport.isPending}
            onClick={handleExport}
          >
            {startExport.isPending
              ? t('lockey_common_loading', { ns: 'common' })
              : t('lockey_contacts_export_start')}
          </Button>
        </CardContent>
      </Card>

      {exportResult && (
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_contacts_export_status')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center gap-2">
              <Badge
                variant={
                  exportResult.status === 'Completed'
                    ? 'default'
                    : exportResult.status === 'Failed'
                      ? 'destructive'
                      : 'secondary'
                }
              >
                {t(`lockey_contacts_export_status_${exportResult.status.toLowerCase()}`)}
              </Badge>
              <span className="text-sm text-muted-foreground">
                {t('lockey_contacts_export_form_format')}: {exportResult.format.toUpperCase()}
              </span>
            </div>

            <dl className="space-y-2 text-sm">
              <div>
                <dt className="text-muted-foreground">{t('lockey_contacts_export_created_at')}</dt>
                <dd>{new Date(exportResult.createdAt).toLocaleString(i18n.language)}</dd>
              </div>
              {exportResult.completedAt && (
                <div>
                  <dt className="text-muted-foreground">{t('lockey_contacts_export_completed_at')}</dt>
                  <dd>{new Date(exportResult.completedAt).toLocaleString(i18n.language)}</dd>
                </div>
              )}
            </dl>

            {exportResult.downloadUrl && (
              <Button type="button" asChild>
                <a
                  href={exportResult.downloadUrl}
                  download
                  rel="noopener noreferrer"
                >
                  {t('lockey_contacts_export_download')}
                </a>
              </Button>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
