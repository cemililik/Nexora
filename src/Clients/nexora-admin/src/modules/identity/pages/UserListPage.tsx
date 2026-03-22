import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useUsers } from '../hooks/useUsers';
import { UserStatusBadge } from '../components/UserStatusBadge';
import type { UserDto } from '../types';

export default function UserListPage() {
  const { t, i18n } = useTranslation('identity');
  const { page, pageSize, setPage } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { data, isPending } = useUsers({ page, pageSize });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_users' },
    ]);
  }, [setBreadcrumbs]);

  const columns: ColumnDef<UserDto>[] = [
    {
      key: 'name',
      header: t('lockey_identity_col_name'),
      render: (row) => (
        <Link
          to={`/identity/users/${row.id}`}
          className="font-medium text-primary hover:underline"
        >
          {row.firstName} {row.lastName}
        </Link>
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
      render: (row) =>
        row.lastLoginAt
          ? new Date(row.lastLoginAt).toLocaleDateString(i18n.language)
          : t('lockey_identity_never'),
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

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        isLoading={isPending}
        emptyMessage={t('lockey_identity_empty_users')}
      />
    </div>
  );
}
