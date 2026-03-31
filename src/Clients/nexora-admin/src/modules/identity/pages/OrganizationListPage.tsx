import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { Building } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { SearchInput } from '@/shared/components/data/SearchInput';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useOrganizations } from '../hooks/useOrganizations';
import type { OrganizationDto } from '../types';

export default function OrganizationListPage() {
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const { page, pageSize, setPage, setPageSize } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const [orgSearch, setOrgSearch] = useState('');
  const { data, isPending } = useOrganizations({ page, pageSize });

  const filteredItems = useMemo(() => {
    const items = data?.items ?? [];
    if (!orgSearch) return items;
    const lower = orgSearch.toLowerCase();
    return items.filter((o) =>
      o.name.toLowerCase().includes(lower) || o.slug.toLowerCase().includes(lower),
    );
  }, [data?.items, orgSearch]);

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

      <SearchInput
        value={orgSearch}
        onChange={setOrgSearch}
        placeholder={t('lockey_identity_search_organizations')}
        className="w-72"
      />

      <DataTable
        columns={columns}
        data={filteredItems}
        totalCount={filteredItems.length}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        isLoading={isPending}
        emptyState={
          <EmptyState
            icon={Building}
            title={t('lockey_identity_empty_orgs_title')}
            description={t('lockey_identity_empty_orgs_description')}
            action={{ label: t('lockey_identity_empty_orgs_create'), onClick: () => navigate('/identity/organizations/create') }}
          />
        }
        keyExtractor={(row) => row.id}
        onRowClick={(row) => navigate(`/identity/organizations/${row.id}`)}
      />
    </div>
  );
}
