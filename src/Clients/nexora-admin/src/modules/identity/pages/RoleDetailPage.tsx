import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, useNavigate } from 'react-router';
import { Pencil, Trash2, Save, X, Shield, UserMinus, UserPlus } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { cn } from '@/shared/lib/utils';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { TabContentSkeleton } from '@/shared/components/feedback/TabContentSkeleton';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';
import { RoleStatusBadge } from '../components/RoleStatusBadge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { formatRelativeTime } from '@/shared/lib/date';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useRole, useUpdateRole, useDeleteRole, useRoleUsers, useAddUserToRole, useRemoveUserFromRole } from '../hooks/useRoles';
import { useUsers } from '../hooks/useUsers';
import { PermissionSelector } from '../components/PermissionSelector';
import type { RoleUserDto } from '../types';

type TabKey = 'details' | 'permissions' | 'users';

export default function RoleDetailPage() {
  const { t } = useTranslation('identity');
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<TabKey>('details');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { data: role, isPending } = useRole(id ?? '');
  const { hasPermission } = usePermissions();
  const updateRole = useUpdateRole();
  const deleteRole = useDeleteRole();

  const [editing, setEditing] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editPermissionIds, setEditPermissionIds] = useState<string[]>([]);

  const isDirty = editing && (() => {
    const rolePermIds = new Set(role?.permissions.map((p) => p.id) ?? []);
    return (
      editName !== (role?.name ?? '') ||
      editDescription !== (role?.description ?? '') ||
      editPermissionIds.length !== rolePermIds.size ||
      editPermissionIds.some((permId) => !rolePermIds.has(permId))
    );
  })();
  const { isBlocked: isEditBlocked, proceed: proceedEdit, reset: resetEdit } =
    useUnsavedChangesGuard(isDirty);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_nav_roles', path: '/identity/roles' },
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

  if (isPending || !role) {
    return <LoadingSkeleton />;
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
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Shield className="h-6 w-6 text-muted-foreground" />
          <div>
            <h1 className="text-2xl font-semibold">{role.name}</h1>
            <div className="flex items-center gap-2 mt-1">
              <RoleStatusBadge isActive={role.isActive} />
              {role.isSystemRole && (
                <span className="text-xs text-muted-foreground bg-muted px-2 py-0.5 rounded">
                  {t('lockey_identity_system_role')}
                </span>
              )}
            </div>
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
              {hasPermission('identity.roles.update') && !role.isSystemRole && (
                <Button variant="outline" size="icon" title={t('lockey_identity_action_edit')} onClick={() => setEditing(true)}>
                  <Pencil className="h-4 w-4" />
                </Button>
              )}
              {hasPermission('identity.roles.delete') && !role.isSystemRole && (
                <Button variant="outline" size="icon" title={t('lockey_identity_action_delete')} onClick={() => setDeleteOpen(true)}>
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              )}
            </>
          )}
        </div>
      </div>

      {editing && (
        <div className="rounded-lg border bg-muted/30 p-4 space-y-4">
          <div className="space-y-2">
            <label htmlFor="edit-role-name" className="text-sm font-medium">{t('lockey_identity_form_role_name')}</label>
            <Input
              id="edit-role-name"
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <label htmlFor="edit-role-desc" className="text-sm font-medium">{t('lockey_identity_role_description')}</label>
            <Input
              id="edit-role-desc"
              value={editDescription}
              onChange={(e) => setEditDescription(e.target.value)}
              placeholder={t('lockey_identity_role_description')}
            />
          </div>
        </div>
      )}

      {/* Tab navigation */}
      <div className="flex gap-1 border-b">
        {([
          { key: 'details' as const, label: t('lockey_identity_tab_details') },
          { key: 'permissions' as const, label: t('lockey_identity_tab_permissions') },
          { key: 'users' as const, label: t('lockey_identity_tab_users') },
        ]).map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => { setActiveTab(tab.key); window.scrollTo({ top: 0, behavior: 'smooth' }); }}
            className={cn(
              'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
              activeTab === tab.key
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'details' && (
        <div className="mt-4 space-y-2 text-sm">
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
        </div>
      )}

      {activeTab === 'permissions' && (
        <div className="mt-4">
          {editing ? (
            <PermissionSelector
              selectedIds={editPermissionIds}
              onChange={setEditPermissionIds}
            />
          ) : (
            <PermissionReadOnly permissions={role.permissions} />
          )}
        </div>
      )}

      {activeTab === 'users' && (
        <div className="mt-4">
          <RoleUsersPanel roleId={role.id} />
        </div>
      )}

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

      {/* Unsaved Changes Guard */}
      <ConfirmDialog
        open={isEditBlocked}
        onOpenChange={(open) => { if (!open) resetEdit(); }}
        title={t('lockey_common_unsaved_changes_title', { ns: 'common' })}
        description={t('lockey_common_unsaved_changes_description', { ns: 'common' })}
        onConfirm={proceedEdit}
        confirmLabel={t('lockey_common_leave', { ns: 'common' })}
        cancelLabel={t('lockey_common_stay', { ns: 'common' })}
        variant="destructive"
      />
    </div>
  );
}

/* ── Role Users Panel (inline, not dialog) ── */

function RoleUsersPanel({
  roleId,
}: {
  roleId: string;
}) {
  const { t } = useTranslation('identity');
  const { hasPermission } = usePermissions();
  const [addUserOpen, setAddUserOpen] = useState(false);
  const [userToRemove, setUserToRemove] = useState<RoleUserDto | null>(null);

  const { data: roleUsersData, isPending } = useRoleUsers(roleId, { page: 1, pageSize: 50 });
  const removeUserFromRole = useRemoveUserFromRole(roleId);

  const handleRemove = () => {
    if (!userToRemove) return;
    removeUserFromRole.mutate(userToRemove.userId, {
      onSuccess: () => setUserToRemove(null),
      onError: () => setUserToRemove(null),
    });
  };

  return (
    <div className="space-y-4">
      {hasPermission('identity.roles.update') && (
        <div className="flex justify-end">
          <Button size="sm" onClick={() => setAddUserOpen(true)}>
            <UserPlus className="h-4 w-4 me-1" />
            {t('lockey_identity_role_add_user')}
          </Button>
        </div>
      )}

      <div className="space-y-1">
        {isPending ? (
          <TabContentSkeleton variant="list" />
        ) : roleUsersData?.items.length === 0 ? (
          <p className="text-sm text-muted-foreground py-4 text-center">
            {t('lockey_identity_role_no_users')}
          </p>
        ) : (
          roleUsersData?.items.map((user) => (
            <div
              key={`${user.userId}-${user.organizationId}`}
              className="flex items-center justify-between rounded-md px-3 py-2 text-sm hover:bg-accent/50 transition-colors"
            >
              <div>
                <p className="font-medium">
                  {user.firstName} {user.lastName}
                </p>
                <p className="text-xs text-muted-foreground">{user.email}</p>
                <p className="text-xs text-muted-foreground">
                  {t('lockey_identity_col_assigned')}: {formatRelativeTime(user.assignedAt)}
                </p>
              </div>
              {hasPermission('identity.roles.update') && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  title={t('lockey_identity_role_remove_user')}
                  onClick={() => setUserToRemove(user)}
                >
                  <UserMinus className="h-4 w-4 text-destructive" />
                </Button>
              )}
            </div>
          ))
        )}
      </div>

      {/* Remove user confirmation */}
      <Dialog open={userToRemove !== null} onOpenChange={() => setUserToRemove(null)}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>{t('lockey_identity_role_remove_user')}</DialogTitle>
            <DialogDescription>
              {t('lockey_identity_confirm_remove_user_from_role', {
                userName: userToRemove ? `${userToRemove.firstName} ${userToRemove.lastName}` : '',
              })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setUserToRemove(null)}>
              {t('lockey_identity_cancel')}
            </Button>
            <Button
              variant="destructive"
              onClick={handleRemove}
              disabled={removeUserFromRole.isPending}
            >
              {t('lockey_identity_role_remove_user')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Add user to role */}
      <AddUserToRoleDialog
        open={addUserOpen}
        onOpenChange={setAddUserOpen}
        roleId={roleId}
        existingUserIds={roleUsersData?.items.map((u) => u.userId) ?? []}
      />
    </div>
  );
}

function AddUserToRoleDialog({
  open,
  onOpenChange,
  roleId,
  existingUserIds,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  roleId: string;
  existingUserIds: string[];
}) {
  const { t } = useTranslation('identity');
  const [search, setSearch] = useState('');
  const { data: usersData } = useUsers({ page: 1, pageSize: 50 });
  const addUserToRole = useAddUserToRole(roleId);

  const availableUsers =
    usersData?.items.filter((u) => !existingUserIds.includes(u.id)) ?? [];

  const filtered = search
    ? availableUsers.filter(
        (u) =>
          u.firstName.toLowerCase().includes(search.toLowerCase()) ||
          u.lastName.toLowerCase().includes(search.toLowerCase()) ||
          u.email.toLowerCase().includes(search.toLowerCase()),
      )
    : availableUsers;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('lockey_identity_role_add_user')}</DialogTitle>
          <DialogDescription className="sr-only">
            {t('lockey_identity_role_add_user')}
          </DialogDescription>
        </DialogHeader>
        <Input
          placeholder={t('lockey_identity_search_users')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <div className="max-h-60 overflow-y-auto space-y-1">
          {filtered.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              {t('lockey_identity_no_users_available')}
            </p>
          ) : (
            filtered.map((user) => (
              <button
                key={user.id}
                type="button"
                disabled={addUserToRole.isPending}
                onClick={() =>
                  addUserToRole.mutate(user.id, {
                    onSuccess: () => onOpenChange(false),
                  })
                }
                className="flex w-full items-center justify-between rounded-md px-3 py-2 text-sm hover:bg-accent transition-colors"
              >
                <div className="text-left">
                  <p className="font-medium">
                    {user.firstName} {user.lastName}
                  </p>
                  <p className="text-xs text-muted-foreground">{user.email}</p>
                </div>
              </button>
            ))
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t('lockey_identity_cancel')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function PermissionReadOnly({ permissions }: { permissions: Array<{ module: string; resource: string; action: string }> }) {
  const { t } = useTranslation(['identity', 'common']);
  const grouped = permissions.reduce<Record<string, Array<{ resource: string; action: string }>>>((acc, p) => {
    (acc[p.module] ??= []).push(p);
    return acc;
  }, {});

  return (
    <div className="space-y-3">
      {Object.entries(grouped).sort(([a], [b]) => a.localeCompare(b)).map(([module, perms]) => (
        <div key={module}>
          <p className="text-sm font-medium capitalize mb-1">{t('lockey_common_module_' + module, { ns: 'common', defaultValue: module })}</p>
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
        <p className="text-sm text-muted-foreground">{t('lockey_identity_no_permissions')}</p>
      )}
    </div>
  );
}
