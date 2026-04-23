import { useCallback, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { Building2 } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { SearchInput } from '@/shared/components/data/SearchInput';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { formatRelativeTime } from '@/shared/lib/date';
import { useTenants } from '../hooks/useTenants';
import { TenantStatusBadge } from '../components/UserStatusBadge';
import type { TenantDto } from '../types';

export default function TenantListPage() {
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const { page, pageSize, setPage, setPageSize } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const [searchParams, setSearchParams] = useSearchParams();

  const search = searchParams.get('search') ?? undefined;

  const handleSearchChange = useCallback(
    (value: string) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (value) { next.set('search', value); } else { next.delete('search'); }
        next.set('page', '1');
        return next;
      });
    },
    [setSearchParams],
  );

  const { data, isPending, isError } = useTenants({ page, pageSize, search });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_tenants' },
    ]);
  }, [setBreadcrumbs]);

  const hasActiveFilters = !!search;

  const emptyStateNode = useMemo(() => {
    if (hasActiveFilters) {
      return (
        <EmptyState
          icon={Building2}
          title={t('lockey_common_no_results_filtered', { ns: 'common' })}
          action={{
            label: t('lockey_common_reset_filters', { ns: 'common' }),
            onClick: () => setSearchParams(new URLSearchParams()),
          }}
        />
      );
    }
    return (
      <EmptyState
        icon={Building2}
        title={t('lockey_identity_empty_tenants')}
        action={
          hasPermission('identity.tenants.manage')
            ? { label: t('lockey_identity_tenants_create'), onClick: () => navigate('/identity/tenants/create') }
            : undefined
        }
      />
    );
  }, [hasActiveFilters, t, navigate, setSearchParams, hasPermission]);

  const columns: ColumnDef<TenantDto>[] = [
    {
      key: 'name',
      header: t('lockey_identity_col_tenant_name'),
      render: (row) => <span className="font-medium">{row.name}</span>,
    },
    { key: 'slug', header: t('lockey_identity_col_slug'), render: (row) => row.slug },
    {
      key: 'status',
      header: t('lockey_identity_col_status'),
      render: (row) => <TenantStatusBadge status={row.status} />,
    },
    {
      key: 'createdAt',
      header: t('lockey_identity_col_created_at'),
      render: (row) => formatRelativeTime(row.createdAt),
    },
  ];

  if (isError) {
    return (
      <div className="flex min-h-[200px] flex-col items-center justify-center gap-4 p-8">
        <p className="text-muted-foreground">
          {t('lockey_error_something_went_wrong', { ns: 'error' })}
        </p>
        <Button type="button" onClick={() => window.location.reload()}>
          {t('lockey_common_try_again', { ns: 'common' })}
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_identity_tenants_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_identity_tenants_description')}
          </p>
        </div>
        {hasPermission('identity.tenants.manage') && (
          <Button type="button" asChild>
            <Link to="/identity/tenants/create">{t('lockey_identity_tenants_create')}</Link>
          </Button>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <SearchInput
          value={search ?? ''}
          onChange={handleSearchChange}
          placeholder={t('lockey_identity_search_tenants')}
          className="w-64"
        />
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
        emptyState={emptyStateNode}
        keyExtractor={(row) => row.id}
        onRowClick={(row) => navigate(`/identity/tenants/${row.id}`)}
      />
    </div>
  );
}
