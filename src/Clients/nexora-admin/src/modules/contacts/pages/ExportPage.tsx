import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/components/ui/select';
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
            <Select value={format} onValueChange={(val: string) => setFormat(val as ExportFormat)}>
              <SelectTrigger className="mt-1">
                <SelectValue placeholder={t('lockey_contacts_export_form_format')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="csv">{t('lockey_contacts_format_csv')}</SelectItem>
                <SelectItem value="xlsx">{t('lockey_contacts_format_xlsx')}</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div>
            <label className="text-sm font-medium">{t('lockey_contacts_export_form_status_filter')}</label>
            <Select
              value={statusFilter ?? '__all__'}
              onValueChange={(val: string) =>
                setStatusFilter(val === '__all__' ? undefined : (val as ContactStatus))
              }
            >
              <SelectTrigger className="mt-1">
                <SelectValue placeholder={t('lockey_contacts_filter_all_statuses')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all__">{t('lockey_contacts_filter_all_statuses')}</SelectItem>
                <SelectItem value="Active">{t('lockey_contacts_status_active')}</SelectItem>
                <SelectItem value="Archived">{t('lockey_contacts_status_archived')}</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div>
            <label className="text-sm font-medium">{t('lockey_contacts_export_form_type_filter')}</label>
            <Select
              value={typeFilter ?? '__all__'}
              onValueChange={(val: string) =>
                setTypeFilter(val === '__all__' ? undefined : (val as ContactType))
              }
            >
              <SelectTrigger className="mt-1">
                <SelectValue placeholder={t('lockey_contacts_filter_all_types')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all__">{t('lockey_contacts_filter_all_types')}</SelectItem>
                <SelectItem value="Individual">{t('lockey_contacts_type_individual')}</SelectItem>
                <SelectItem value="Organization">{t('lockey_contacts_type_organization')}</SelectItem>
              </SelectContent>
            </Select>
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
