import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useUser, useUpdateProfile, useUpdateUserStatus, useDeleteUser, useUserRoles, useAssignUserRoles, userKeys } from '../hooks/useUsers';
import { toast } from 'sonner';
import { useRoles } from '../hooks/useRoles';
import { useOrganizations, useRemoveMember, orgKeys } from '../hooks/useOrganizations';
import { api } from '@/shared/lib/api';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { Input } from '@/shared/components/ui/input';
import { UserStatusBadge } from '../components/UserStatusBadge';
import { UserForm } from '../components/UserForm';
import type { RoleDto, OrganizationDto, UserOrganizationDto } from '../types';

export default function UserDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('identity');
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();

  const { data: user, isPending } = useUser(id);
  const updateProfile = useUpdateProfile(id);
  const updateStatus = useUpdateUserStatus(id);
  const deleteUser = useDeleteUser();
  const { data: allRoles } = useRoles();
  const { data: allOrgs } = useOrganizations({ page: 1, pageSize: 100 });

  const [isEditing, setIsEditing] = useState(false);
  const [confirmAction, setConfirmAction] = useState<'activate' | 'deactivate' | 'delete' | null>(null);
  const [addOrgOpen, setAddOrgOpen] = useState(false);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_users', path: '/identity/users' },
      { label: user ? `${user.firstName} ${user.lastName}` : '...' },
    ]);
  }, [setBreadcrumbs, user]);

  if (isPending) return <LoadingSkeleton lines={8} />;
  if (!user) return null;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">
            {user.firstName} {user.lastName}
          </h1>
          <p className="text-sm text-muted-foreground">{user.email}</p>
        </div>
        <div className="flex gap-2">
          {hasPermission('identity.users.update') && (
            <>
              {user.status === 'Active' ? (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setConfirmAction('deactivate')}
                >
                  {t('lockey_identity_action_deactivate')}
                </Button>
              ) : user.status === 'Inactive' ? (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setConfirmAction('activate')}
                >
                  {t('lockey_identity_action_activate')}
                </Button>
              ) : null}
            </>
          )}
          {hasPermission('identity.users.update') && (
            <Button
              type="button"
              variant={isEditing ? 'outline' : 'default'}
              onClick={() => setIsEditing(!isEditing)}
            >
              {isEditing ? t('lockey_common_cancel', { ns: 'common' }) : t('lockey_identity_action_edit')}
            </Button>
          )}
          {hasPermission('identity.users.delete') && (
            <Button
              type="button"
              variant="outline"
              size="icon"
              title={t('lockey_identity_action_delete_user')}
              onClick={() => setConfirmAction('delete')}
            >
              <Trash2 className="h-4 w-4 text-destructive" />
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_identity_tab_profile')}</CardTitle>
          </CardHeader>
          <CardContent>
            {isEditing ? (
              <UserForm
                mode="edit"
                defaultValues={{
                  firstName: user.firstName,
                  lastName: user.lastName,
                  phone: user.phone,
                }}
                onSubmit={(data) => {
                  updateProfile.mutate(data, {
                    onSuccess: () => setIsEditing(false),
                    onError: (err) => handleApiError(err),
                  });
                }}
                isPending={updateProfile.isPending}
              />
            ) : (
              <dl className="space-y-3">
                <div>
                  <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_status')}</dt>
                  <dd><UserStatusBadge status={user.status} /></dd>
                </div>
                <div>
                  <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_phone')}</dt>
                  <dd>{user.phone ?? '—'}</dd>
                </div>
                <div>
                  <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_last_login')}</dt>
                  <dd>
                    {user.lastLoginAt
                      ? new Date(user.lastLoginAt).toLocaleString(i18n.language)
                      : t('lockey_identity_never')}
                  </dd>
                </div>
              </dl>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>{t('lockey_identity_tab_organizations')}</CardTitle>
            <Button size="sm" onClick={() => setAddOrgOpen(true)}>
              {t('lockey_identity_action_join_org')}
            </Button>
          </CardHeader>
          <CardContent>
            {user.organizations.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {t('lockey_identity_empty_orgs')}
              </p>
            ) : (
              <ul className="space-y-2">
                {user.organizations.map((org) => (
                  <UserOrgRow key={org.organizationId} userId={id} org={org} />
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Roles per organization */}
      <Card>
        <CardHeader>
          <CardTitle>{t('lockey_identity_tab_roles')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {user.organizations.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {t('lockey_identity_roles_need_org')}
            </p>
          ) : (
            user.organizations.map((org) => (
              <UserOrgRoles
                key={org.organizationId}
                userId={id}
                organizationId={org.organizationId}
                organizationName={org.organizationName}
                allRoles={allRoles ?? []}
              />
            ))
          )}
        </CardContent>
      </Card>

      <ConfirmDialog
        open={confirmAction !== null}
        onOpenChange={() => setConfirmAction(null)}
        title={
          confirmAction === 'delete'
            ? t('lockey_identity_action_delete_user')
            : confirmAction === 'deactivate'
              ? t('lockey_identity_action_deactivate')
              : t('lockey_identity_action_activate')
        }
        description={
          confirmAction === 'delete'
            ? t('lockey_identity_confirm_delete_user')
            : confirmAction === 'deactivate'
              ? t('lockey_identity_confirm_deactivate_user')
              : t('lockey_identity_confirm_activate_user')
        }
        variant={confirmAction === 'delete' || confirmAction === 'deactivate' ? 'destructive' : 'default'}
        onConfirm={() => {
          if (confirmAction === 'delete') {
            deleteUser.mutate(id, {
              onSuccess: () => {
                setConfirmAction(null);
                void navigate('/identity/users');
              },
            });
            return;
          }
          if (confirmAction === 'activate') {
            updateStatus.activate();
          } else {
            updateStatus.deactivate();
          }
          setConfirmAction(null);
        }}
        isPending={updateStatus.isPending || deleteUser.isPending}
      />

      <AddToOrgDialog
        open={addOrgOpen}
        onOpenChange={setAddOrgOpen}
        userId={id}
        existingOrgIds={user.organizations.map((o) => o.organizationId)}
        allOrgs={allOrgs?.items ?? []}
      />
    </div>
  );
}

/* ── Org Row with leave button ── */

function UserOrgRow({ userId, org }: { userId: string; org: UserOrganizationDto }) {
  const { t } = useTranslation('identity');
  const removeMember = useRemoveMember(org.organizationId);
  const { handleApiError } = useApiError();

  return (
    <li className="flex items-center justify-between">
      <div className="flex items-center gap-2">
        <span>{org.organizationName}</span>
        {org.isDefault && (
          <Badge variant="secondary">{t('lockey_common_default', { ns: 'common' })}</Badge>
        )}
      </div>
      <Button
        size="sm"
        variant="ghost"
        className="text-destructive"
        disabled={removeMember.isPending}
        onClick={() => {
          removeMember.mutate(userId, {
            onError: (err) => handleApiError(err),
          });
        }}
      >
        {t('lockey_identity_action_leave_org')}
      </Button>
    </li>
  );
}

/* ── Add to Org Dialog ── */

function AddToOrgDialog({
  open,
  onOpenChange,
  userId,
  existingOrgIds,
  allOrgs,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  userId: string;
  existingOrgIds: string[];
  allOrgs: OrganizationDto[];
}) {
  const { t } = useTranslation('identity');
  const { handleApiError } = useApiError();
  const [search, setSearch] = useState('');
  const existing = new Set(existingOrgIds);
  const available = allOrgs.filter((o) => !existing.has(o.id) && o.isActive);

  const filtered = search
    ? available.filter((o) => o.name.toLowerCase().includes(search.toLowerCase()))
    : available;

  const queryClient = useQueryClient();
  const addMember = useMutation({
    mutationFn: (orgId: string) =>
      api.post(`/identity/organizations/${encodeURIComponent(orgId)}/members`, { userId }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      void queryClient.invalidateQueries({ queryKey: orgKeys.all });
      toast.success(t('lockey_identity_member_added'));
      onOpenChange(false);
    },
    onError: (err) => handleApiError(err),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t('lockey_identity_action_join_org')}</DialogTitle>
          <DialogDescription className="sr-only">{t('lockey_identity_action_join_org')}</DialogDescription>
        </DialogHeader>
        <Input
          placeholder={t('lockey_identity_search_orgs')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <div className="max-h-60 overflow-y-auto space-y-1">
          {filtered.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              {t('lockey_identity_no_orgs_available')}
            </p>
          ) : (
            filtered.map((org) => (
              <button
                key={org.id}
                type="button"
                disabled={addMember.isPending}
                onClick={() => addMember.mutate(org.id)}
                className="flex w-full items-center rounded-md px-3 py-2 text-sm hover:bg-accent transition-colors"
              >
                {org.name}
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

/* ── Per-Org Role Assignment ── */

function UserOrgRoles({
  userId,
  organizationId,
  organizationName,
  allRoles,
}: {
  userId: string;
  organizationId: string;
  organizationName: string;
  allRoles: RoleDto[];
}) {
  const { t } = useTranslation('identity');
  const { data: userRoles, isLoading } = useUserRoles(userId, organizationId);
  const assignRoles = useAssignUserRoles(userId);
  const [editing, setEditing] = useState(false);
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>([]);

  useEffect(() => {
    if (userRoles) {
      setSelectedRoleIds(userRoles.map((r) => r.id));
    }
  }, [userRoles]);

  const handleSave = () => {
    assignRoles.mutate(
      { organizationId, roleIds: selectedRoleIds },
      { onSuccess: () => setEditing(false) },
    );
  };

  const toggleRole = (roleId: string) => {
    setSelectedRoleIds((prev) =>
      prev.includes(roleId) ? prev.filter((id) => id !== roleId) : [...prev, roleId],
    );
  };

  return (
    <div className="rounded-md border p-3 space-y-2">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium">{organizationName}</p>
        {editing ? (
          <div className="flex gap-1">
            <Button size="sm" variant="outline" onClick={() => setEditing(false)}>
              {t('lockey_identity_cancel')}
            </Button>
            <Button size="sm" onClick={handleSave} disabled={assignRoles.isPending}>
              {t('lockey_identity_save')}
            </Button>
          </div>
        ) : (
          <Button size="sm" variant="outline" onClick={() => setEditing(true)}>
            {t('lockey_identity_action_edit')}
          </Button>
        )}
      </div>

      {isLoading ? (
        <p className="text-xs text-muted-foreground">{t('lockey_identity_loading')}</p>
      ) : editing ? (
        <div className="space-y-1">
          {allRoles.map((role) => (
            <label key={role.id} className="flex items-center gap-2 text-sm cursor-pointer">
              <input
                type="checkbox"
                checked={selectedRoleIds.includes(role.id)}
                onChange={() => toggleRole(role.id)}
                className="h-3.5 w-3.5 rounded border-input"
              />
              <span>{role.name}</span>
              {role.isSystemRole && (
                <span className="text-xs text-muted-foreground">({t('lockey_identity_col_system_role')})</span>
              )}
            </label>
          ))}
        </div>
      ) : (
        <div className="flex flex-wrap gap-1">
          {userRoles && userRoles.length > 0 ? (
            userRoles.map((r) => (
              <Badge key={r.id} variant="secondary">{r.name}</Badge>
            ))
          ) : (
            <p className="text-xs text-muted-foreground">{t('lockey_identity_no_roles')}</p>
          )}
        </div>
      )}
    </div>
  );
}
