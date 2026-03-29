import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useOrganizations } from '../hooks/useOrganizations';
import type { OrganizationDto } from '../types';

export default function OrganizationListPage() {
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { data, isPending } = useOrganizations({ page, pageSize });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_organizations' },
    ]);
  }, [setBreadcrumbs]);

  const columns: ColumnDef<OrganizationDto>[] = [
    {
      key: 'name',
      header: t('lockey_identity_col_org_name'),
      render: (row) => (
        <span className="font-medium">{row.name}</span>
      ),
    },
    { key: 'slug', header: t('lockey_identity_col_slug'), render: (row) => row.slug },
    { key: 'timezone', header: t('lockey_identity_col_timezone'), render: (row) => row.timezone },
    { key: 'currency', header: t('lockey_identity_col_currency'), render: (row) => row.defaultCurrency },
    {
      key: 'active',
      header: t('lockey_identity_col_active'),
      render: (row) => (
        <Badge variant={row.isActive ? 'default' : 'secondary'}>
          {row.isActive ? t('lockey_identity_yes') : t('lockey_identity_no')}
        </Badge>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_identity_orgs_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_identity_orgs_description')}
          </p>
        </div>
        <Button type="button" asChild>
          <Link to="/identity/organizations/create">
            {t('lockey_identity_orgs_create')}
          </Link>
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
        emptyMessage={t('lockey_identity_empty_orgs')}
        keyExtractor={(row) => row.id}
        onRowClick={(row) => navigate(`/identity/organizations/${row.id}`)}
      />
    </div>
  );
}
