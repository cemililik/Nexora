import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useTenants } from '../hooks/useTenants';
import { TenantStatusBadge } from '../components/UserStatusBadge';
import type { TenantDto } from '../types';

export default function TenantListPage() {
  const { t, i18n } = useTranslation('identity');
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { data, isPending, isError, error } = useTenants({ page, pageSize });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_tenants' },
    ]);
  }, [setBreadcrumbs]);

  const columns: ColumnDef<TenantDto>[] = [
    {
      key: 'name',
      header: t('lockey_identity_col_tenant_name'),
      render: (row) => (
        <Link
          to={`/identity/tenants/${row.id}`}
          className="font-medium text-primary hover:underline"
        >
          {row.name}
        </Link>
      ),
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
      render: (row) => new Date(row.createdAt).toLocaleDateString(i18n.language),
    },
  ];

  if (isError) {
    return (
      <div className="flex min-h-[200px] flex-col items-center justify-center gap-4 p-8">
        <p className="text-muted-foreground">
          {t('lockey_error_something_went_wrong', { ns: 'error' })}
        </p>
        <p className="text-sm text-muted-foreground">{error?.message}</p>
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
        <Button type="button" asChild>
          <Link to="/identity/tenants/create">{t('lockey_identity_tenants_create')}</Link>
        </Button>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        isLoading={isPending}
        emptyMessage={t('lockey_identity_empty_tenants')}
      />
    </div>
  );
}
