import { useCallback, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useSearchParams } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { formatRelativeTime } from '@/shared/lib/date';
import { useUsers } from '../hooks/useUsers';
import { useOrganizations } from '../hooks/useOrganizations';
import { useRoles } from '../hooks/useRoles';
import { UserStatusBadge } from '../components/UserStatusBadge';
import type { UserDto } from '../types';

export default function UserListPage() {
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const { page, pageSize, setPage, setPageSize } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const [searchParams, setSearchParams] = useSearchParams();

  const organizationId = searchParams.get('organizationId') ?? undefined;
  const roleId = searchParams.get('roleId') ?? undefined;
  const search = searchParams.get('search') ?? undefined;

  const updateFilter = useCallback(
    (key: string, value: string) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (value) {
          next.set(key, value);
        } else {
          next.delete(key);
        }
        next.set('page', '1');
        return next;
      });
    },
    [setSearchParams],
  );

  const { data, isPending } = useUsers({ page, pageSize, organizationId, roleId, search });
  const { data: organizations } = useOrganizations({ page: 1, pageSize: 100 });
  const { data: roles } = useRoles();

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_users' },
    ]);
  }, [setBreadcrumbs]);

  const handleOrganizationChange = useCallback(
    (value: string) => {
      updateFilter('organizationId', value === '__all__' ? '' : value);
    },
    [updateFilter],
  );

  const handleRoleChange = useCallback(
    (value: string) => {
      updateFilter('roleId', value === '__all__' ? '' : value);
    },
    [updateFilter],
  );

  const handleSearchChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      updateFilter('search', e.target.value);
    },
    [updateFilter],
  );

  const columns: ColumnDef<UserDto>[] = [
    {
      key: 'name',
      header: t('lockey_identity_col_name'),
      render: (row) => (
        <span className="font-medium">{row.firstName} {row.lastName}</span>
      ),
    },
    { key: 'email', header: t('lockey_identity_col_email'), render: (row) => row.email },
    {
      key: 'status',
      header: t('lockey_identity_col_status'),
      render: (row) => <UserStatusBadge status={row.status} />,
    },
    {
      key: 'lastLoginAt',
      header: t('lockey_identity_col_last_login'),
      render: (row) => formatRelativeTime(row.lastLoginAt, t('lockey_identity_never')),
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_identity_users_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_identity_users_description')}
          </p>
        </div>
        <Button type="button" asChild>
          <Link to="/identity/users/create">{t('lockey_identity_users_create')}</Link>
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <Input
          type="text"
          value={search ?? ''}
          onChange={handleSearchChange}
          placeholder={t('lockey_identity_search_users')}
          className="w-64"
        />
        <Select
          value={organizationId ?? '__all__'}
          onValueChange={handleOrganizationChange}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_identity_filter_all_organizations')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_identity_filter_all_organizations')}</SelectItem>
            {organizations?.items.map((org) => (
              <SelectItem key={org.id} value={org.id}>
                {org.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={roleId ?? '__all__'}
          onValueChange={handleRoleChange}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_identity_filter_all_roles')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_identity_filter_all_roles')}</SelectItem>
            {roles?.map((role) => (
              <SelectItem key={role.id} value={role.id}>
                {role.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
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
        emptyMessage={t('lockey_identity_empty_users')}
        keyExtractor={(row) => row.id}
        onRowClick={(row) => navigate(`/identity/users/${row.id}`)}
      />
    </div>
  );
}
