import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';

import { Input } from '@/shared/components/ui/input';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useAuditLogs } from '../hooks/useAuditLogs';
import type { AuditLogDto } from '../types';

export default function AuditLogPage() {
  const { t } = useTranslation('identity');
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const [searchParams, setSearchParams] = useSearchParams();

  const filterAction = searchParams.get('action') ?? undefined;
  const filterFrom = searchParams.get('from') ?? undefined;
  const filterTo = searchParams.get('to') ?? undefined;

  const { data, isPending } = useAuditLogs({
    action: filterAction,
    from: filterFrom,
    to: filterTo,
    page,
    pageSize,
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_audit_logs' },
    ]);
  }, [setBreadcrumbs]);

  const updateFilter = (key: string, value: string) => {
    const params = new URLSearchParams(searchParams);
    if (value) {
      params.set(key, value);
    } else {
      params.delete(key);
    }
    setSearchParams(params);
  };

  const columns: ColumnDef<AuditLogDto>[] = [
    {
      key: 'action',
      header: t('lockey_identity_col_action'),
      render: (row) => row.action,
    },
    {
      key: 'ipAddress',
      header: t('lockey_identity_col_ip_address'),
      render: (row) => row.ipAddress ?? '—',
    },
    {
      key: 'timestamp',
      header: t('lockey_identity_col_timestamp'),
      render: (row) => new Date(row.timestamp).toLocaleString(),
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('lockey_identity_audit_title')}</h1>
        <p className="text-sm text-muted-foreground">
          {t('lockey_identity_audit_description')}
        </p>
      </div>

      <div className="flex flex-wrap gap-4">
        <div className="space-y-1">
          <label htmlFor="filterAction" className="text-xs text-muted-foreground">
            {t('lockey_identity_audit_filter_action')}
          </label>
          <Input
            id="filterAction"
            className="w-48"
            value={filterAction ?? ''}
            onChange={(e) => updateFilter('action', e.target.value)}
          />
        </div>
        <div className="space-y-1">
          <label htmlFor="filterFrom" className="text-xs text-muted-foreground">
            {t('lockey_identity_audit_filter_from')}
          </label>
          <Input
            id="filterFrom"
            type="date"
            className="w-48"
            value={filterFrom ?? ''}
            onChange={(e) => updateFilter('from', e.target.value)}
          />
        </div>
        <div className="space-y-1">
          <label htmlFor="filterTo" className="text-xs text-muted-foreground">
            {t('lockey_identity_audit_filter_to')}
          </label>
          <Input
            id="filterTo"
            type="date"
            className="w-48"
            value={filterTo ?? ''}
            onChange={(e) => updateFilter('to', e.target.value)}
          />
        </div>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        isLoading={isPending}
        emptyMessage={t('lockey_identity_empty_audit')}
      />
    </div>
  );
}
