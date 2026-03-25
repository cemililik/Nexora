import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, useNavigate } from 'react-router';
import { Pencil, Trash2, Save, X, Shield, Users } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useRole, useUpdateRole, useDeleteRole } from '../hooks/useRoles';
import { PermissionSelector } from '../components/PermissionSelector';

export default function RoleDetailPage() {
  const { t } = useTranslation('identity');
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { data: role, isLoading } = useRole(id ?? '');
  const updateRole = useUpdateRole();
  const deleteRole = useDeleteRole();

  const [editing, setEditing] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editPermissionIds, setEditPermissionIds] = useState<string[]>([]);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_nav_roles', href: '/identity/roles' },
      { label: role?.name ?? '...' },
    ]);
  }, [setBreadcrumbs, role?.name]);

  useEffect(() => {
    if (role) {
      setEditName(role.name);
      setEditDescription(role.description ?? '');
      setEditPermissionIds(role.permissions.map((p) => p.id));
    }
  }, [role]);

  if (isLoading || !role) {
    return <p className="text-muted-foreground">{t('lockey_identity_loading')}</p>;
  }

  const handleSave = () => {
    updateRole.mutate(
      {
        id: role.id,
        name: editName,
        description: editDescription || undefined,
        permissionIds: editPermissionIds,
      },
      { onSuccess: () => setEditing(false) },
    );
  };

  const handleDelete = () => {
    deleteRole.mutate(role.id, {
      onSuccess: () => {
        setDeleteOpen(false);
        void navigate('/identity/roles');
      },
    });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Shield className="h-6 w-6 text-muted-foreground" />
          <div>
            {editing ? (
              <Input
                value={editName}
                onChange={(e) => setEditName(e.target.value)}
                className="text-xl font-bold"
              />
            ) : (
              <h1 className="text-2xl font-bold text-foreground">{role.name}</h1>
            )}
            {role.isSystemRole && (
              <span className="text-xs text-muted-foreground bg-muted px-2 py-0.5 rounded mt-1 inline-block">
                {t('lockey_identity_system_role')}
              </span>
            )}
          </div>
        </div>
        <div className="flex items-center gap-2">
          {editing ? (
            <>
              <Button variant="outline" size="sm" onClick={() => setEditing(false)}>
                <X className="h-4 w-4 me-1" />
                {t('lockey_identity_cancel')}
              </Button>
              <Button size="sm" onClick={handleSave} disabled={updateRole.isPending || !editName}>
                <Save className="h-4 w-4 me-1" />
                {t('lockey_identity_save')}
              </Button>
            </>
          ) : (
            <>
              {!role.isSystemRole && (
                <>
                  <Button variant="outline" size="icon" title={t('lockey_identity_action_edit')} onClick={() => setEditing(true)}>
                    <Pencil className="h-4 w-4" />
                  </Button>
                  <Button variant="outline" size="icon" title={t('lockey_identity_action_delete')} onClick={() => setDeleteOpen(true)}>
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </>
              )}
            </>
          )}
        </div>
      </div>

      {editing && (
        <div className="space-y-2">
          <label className="text-sm font-medium">{t('lockey_identity_role_description')}</label>
          <Input
            value={editDescription}
            onChange={(e) => setEditDescription(e.target.value)}
            placeholder={t('lockey_identity_role_description')}
          />
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <Shield className="h-4 w-4" />
                {t('lockey_identity_permissions')}
                <span className="text-xs text-muted-foreground font-normal">
                  ({editing ? editPermissionIds.length : role.permissions.length})
                </span>
              </CardTitle>
            </CardHeader>
            <CardContent>
              {editing ? (
                <PermissionSelector
                  selectedIds={editPermissionIds}
                  onChange={setEditPermissionIds}
                />
              ) : (
                <PermissionReadOnly permissions={role.permissions} />
              )}
            </CardContent>
          </Card>
        </div>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">{t('lockey_identity_details')}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('lockey_identity_col_name')}</span>
                <span className="text-foreground">{role.name}</span>
              </div>
              {role.description && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">{t('lockey_identity_role_description')}</span>
                  <span className="text-foreground">{role.description?.startsWith('lockey_') ? t(role.description) : role.description}</span>
                </div>
              )}
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('lockey_identity_col_status')}</span>
                <span className="text-foreground">
                  {role.isActive ? t('lockey_identity_active') : t('lockey_identity_inactive')}
                </span>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <Users className="h-4 w-4" />
                {t('lockey_identity_assigned_users')}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-2xl font-bold">{role.assignedUserCount}</p>
              <p className="text-xs text-muted-foreground">{t('lockey_identity_users_with_role')}</p>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Delete confirmation */}
      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>{t('lockey_identity_action_delete')}</DialogTitle>
            <DialogDescription>{t('lockey_identity_confirm_delete_role')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)}>
              {t('lockey_identity_cancel')}
            </Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteRole.isPending}>
              {t('lockey_identity_action_delete')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function PermissionReadOnly({ permissions }: { permissions: Array<{ module: string; resource: string; action: string }> }) {
  const grouped = permissions.reduce<Record<string, Array<{ resource: string; action: string }>>>((acc, p) => {
    (acc[p.module] ??= []).push(p);
    return acc;
  }, {});

  return (
    <div className="space-y-3">
      {Object.entries(grouped).sort(([a], [b]) => a.localeCompare(b)).map(([module, perms]) => (
        <div key={module}>
          <p className="text-sm font-medium capitalize mb-1">{module}</p>
          <div className="flex flex-wrap gap-1">
            {perms.map((p) => (
              <span
                key={`${p.resource}.${p.action}`}
                className="text-xs bg-muted px-2 py-0.5 rounded text-muted-foreground"
              >
                {p.resource}.{p.action}
              </span>
            ))}
          </div>
        </div>
      ))}
      {permissions.length === 0 && (
        <p className="text-sm text-muted-foreground">No permissions assigned</p>
      )}
    </div>
  );
}
