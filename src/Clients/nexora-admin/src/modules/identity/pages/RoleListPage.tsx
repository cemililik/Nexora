import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Badge } from '@/shared/components/ui/badge';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { formatRelativeTime } from '@/shared/lib/date';
import { useRoles, useCreateRole } from '../hooks/useRoles';
import { PermissionSelector } from '../components/PermissionSelector';
import type { RoleDto } from '../types';

function createRoleSchemaFactory(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    name: z.string().trim().min(1, { message: t('lockey_identity_validation_role_name_required') }).max(100, { message: t('lockey_identity_validation_role_name_max') }),
    description: z.string().max(500, { message: t('lockey_identity_validation_role_description_max') }).optional(),
  });
}

export default function RoleListPage() {
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const { page, pageSize, setPage, setPageSize } = usePagination();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { data: roles, isPending, isError, error } = useRoles();
  const createRole = useCreateRole();
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [selectedPermissionIds, setSelectedPermissionIds] = useState<string[]>([]);

  const createRoleSchema = useMemo(() => createRoleSchemaFactory(t), [t]);

  const form = useForm({
    resolver: zodResolver(createRoleSchema),
    defaultValues: { name: '', description: '' },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_roles' },
    ]);
  }, [setBreadcrumbs]);

  const onSubmit = form.handleSubmit((data) => {
    createRole.mutate({ ...data, permissionIds: selectedPermissionIds.length > 0 ? selectedPermissionIds : undefined }, {
      onSuccess: () => {
        setIsDialogOpen(false);
        form.reset();
        setSelectedPermissionIds([]);
      },
      onError: (err) => handleApiError(err, form.setError),
    });
  });

  const columns: ColumnDef<RoleDto>[] = [
    {
      key: 'name',
      header: t('lockey_identity_col_role_name'),
      render: (row) => <span className="font-medium">{row.name}</span>,
    },
    {
      key: 'description',
      header: t('lockey_identity_col_description'),
      render: (row) =>
        row.description?.startsWith('lockey_') ? t(row.description) : (row.description ?? '—'),
    },
    {
      key: 'isSystemRole',
      header: t('lockey_identity_col_system_role'),
      render: (row) => (
        <Badge variant={row.isSystemRole ? 'secondary' : 'outline'}>
          {row.isSystemRole ? t('lockey_identity_yes') : t('lockey_identity_no')}
        </Badge>
      ),
    },
    {
      key: 'permissionsCount',
      header: t('lockey_identity_col_permissions_count'),
      render: (row) => row.permissions.length,
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
        <p className="text-sm text-muted-foreground">{error?.message}</p>
        <Button type="button" onClick={() => window.location.reload()}>
          {t('lockey_common_try_again', { ns: 'common' })}
        </Button>
      </div>
    );
  }

  if (isPending) return <LoadingSkeleton lines={6} />;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_identity_roles_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_identity_roles_description')}
          </p>
        </div>
        {hasPermission('identity.roles.create') && (
          <Button type="button" onClick={() => setIsDialogOpen(true)}>
            {t('lockey_identity_roles_create')}
          </Button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={roles ?? []}
        totalCount={roles?.length ?? 0}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        isLoading={isPending}
        emptyMessage={t('lockey_identity_empty_roles')}
        keyExtractor={(row) => row.id}
        onRowClick={(row) => navigate(`/identity/roles/${row.id}`)}
      />

      <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_identity_roles_create')}</DialogTitle>
            <DialogDescription className="sr-only">{t('lockey_identity_roles_create')}</DialogDescription>
          </DialogHeader>
          <form onSubmit={onSubmit} className="space-y-4">
            <div className="space-y-2">
              <label htmlFor="roleName" className="text-sm font-medium">
                {t('lockey_identity_form_role_name')}
              </label>
              <Input id="roleName" {...form.register('name')} />
              {form.formState.errors.name && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.name.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <label htmlFor="roleDescription" className="text-sm font-medium">
                {t('lockey_identity_form_role_description')}
              </label>
              <Input id="roleDescription" {...form.register('description')} />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('lockey_identity_permissions')}</label>
              <PermissionSelector
                selectedIds={selectedPermissionIds}
                onChange={setSelectedPermissionIds}
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => setIsDialogOpen(false)}
              >
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button type="submit" disabled={createRole.isPending}>
                {createRole.isPending
                  ? t('lockey_common_loading', { ns: 'common' })
                  : t('lockey_identity_roles_create')}
              </Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
