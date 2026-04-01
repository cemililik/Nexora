import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { FileSearch } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { SearchInput } from '@/shared/components/data/SearchInput';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { formatRelativeTime } from '@/shared/lib/date';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { useAuditLogs } from '../hooks/useAuditLogs';
import { useAuditableOperations } from '../hooks/useAuditableOperations';
import { AuditStatusBadge } from '../components/AuditStatusBadge';
import { AuditOperationTypeBadge } from '../components/AuditOperationTypeBadge';
import type { AuditLogDto } from '../types';

export default function AuditLogListPage() {
  const { t } = useTranslation('audit');
  const navigate = useNavigate();
  const { page, pageSize, setPage, setPageSize } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);

  const [searchParams, setSearchParams] = useSearchParams();
  const search = searchParams.get('search') ?? undefined;
  const module = searchParams.get('module') ?? undefined;
  const rawIsSuccess = searchParams.get('isSuccess');
  const isSuccess = rawIsSuccess === 'true' ? true : rawIsSuccess === 'false' ? false : undefined;
  const dateFrom = searchParams.get('dateFrom') ?? undefined;
  const dateTo = searchParams.get('dateTo') ?? undefined;

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_audit_module_name' },
      { label: 'lockey_audit_logs_title' },
    ]);
  }, [setBreadcrumbs]);

  const { data, isPending } = useAuditLogs({ page, pageSize, module, isSuccess, dateFrom, dateTo, search });
  const { data: auditableModules } = useAuditableOperations();

  const moduleNames = useMemo(() => {
    if (!auditableModules) return [];
    return [...new Set(auditableModules.map((m) => m.module))].sort();
  }, [auditableModules]);

  const updateFilter = (key: string, value: string) => {
    setSearchParams((prev: URLSearchParams) => {
      const next = new URLSearchParams(prev);
      if (value) {
        next.set(key, value);
      } else {
        next.delete(key);
      }
      next.set('page', '1');
      return next;
    });
  };

  const hasActiveFilters = !!(search || module || rawIsSuccess || dateFrom || dateTo);
  const handleResetFilters = () => {
    setSearchParams(new URLSearchParams());
  };

  const columns: ColumnDef<AuditLogDto>[] = [
    {
      key: 'timestamp',
      header: t('lockey_audit_col_timestamp'),
      render: (row) => formatRelativeTime(row.timestamp),
    },
    {
      key: 'userEmail',
      header: t('lockey_audit_col_user'),
      render: (row) => row.userEmail,
    },
    {
      key: 'module',
      header: t('lockey_audit_col_module'),
      render: (row) => t('lockey_audit_module_' + row.module, { defaultValue: row.module }),
    },
    {
      key: 'operation',
      header: t('lockey_audit_col_operation'),
      render: (row) => t('lockey_audit_operation_' + row.operation.toLowerCase(), { defaultValue: row.operation }),
    },
    {
      key: 'operationType',
      header: t('lockey_audit_col_type'),
      render: (row) => <AuditOperationTypeBadge operationType={row.operationType} />,
    },
    {
      key: 'isSuccess',
      header: t('lockey_audit_col_status'),
      render: (row) => <AuditStatusBadge isSuccess={row.isSuccess} />,
    },
    {
      key: 'entity',
      header: t('lockey_audit_col_entity'),
      render: (row) => (row.entityType ? `${row.entityType} / ${row.entityId ?? ''}` : '—'),
    },
    {
      key: 'actions',
      header: '',
      render: (row) => (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => navigate(`/audit/logs/${row.id}`)}
        >
          {t('lockey_audit_detail_title')}
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('lockey_audit_logs_title')}</h1>
        <p className="text-sm text-muted-foreground">
          {t('lockey_audit_logs_description')}
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <SearchInput
          value={search ?? ''}
          onChange={(value) => updateFilter('search', value)}
          placeholder={t('lockey_audit_search_placeholder')}
          className="w-64"
        />
        <Select
          value={module ?? '__all__'}
          onValueChange={(v) => updateFilter('module', v === '__all__' ? '' : v)}
        >
          <SelectTrigger className="w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_audit_filter_all_modules')}</SelectItem>
            {moduleNames.map((name) => (
              <SelectItem key={name} value={name}>
                {t('lockey_audit_module_' + name)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={rawIsSuccess ?? '__all__'}
          onValueChange={(v) => updateFilter('isSuccess', v === '__all__' ? '' : v)}
        >
          <SelectTrigger className="w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_audit_filter_all_status')}</SelectItem>
            <SelectItem value="true">{t('lockey_audit_status_success')}</SelectItem>
            <SelectItem value="false">{t('lockey_audit_status_failed')}</SelectItem>
          </SelectContent>
        </Select>
        <div className="flex items-center gap-2">
          <label htmlFor="audit-date-from" className="text-sm text-muted-foreground">{t('lockey_audit_filter_from_date')}</label>
          <Input
            id="audit-date-from"
            type="date"
            value={dateFrom ?? ''}
            onChange={(e) => updateFilter('dateFrom', e.target.value)}
          />
        </div>
        <div className="flex items-center gap-2">
          <label htmlFor="audit-date-to" className="text-sm text-muted-foreground">{t('lockey_audit_filter_to_date')}</label>
          <Input
            id="audit-date-to"
            type="date"
            value={dateTo ?? ''}
            onChange={(e) => updateFilter('dateTo', e.target.value)}
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
        onPageSizeChange={setPageSize}
        isLoading={isPending}
        emptyState={
          <EmptyState
            icon={FileSearch}
            title={t('lockey_audit_empty_title')}
            description={t('lockey_audit_empty_description')}
            action={
              hasActiveFilters
                ? { label: t('lockey_common_reset_filters', { ns: 'common', defaultValue: 'Reset Filters' }), onClick: handleResetFilters }
                : undefined
            }
          />
        }
        keyExtractor={(row) => row.id}
      />
    </div>
  );
}
