import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Input } from '@/shared/components/ui/input';
import { SearchableDropdown } from '@/shared/components/data/SearchableDropdown';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { useUsers } from '@/modules/identity/hooks/useUsers';
import { useRoles } from '@/modules/identity/hooks/useRoles';
import { useFolderAccess, useGrantFolderAccess, useRevokeFolderAccess } from '../hooks/useFolderAccess';
import type { AccessPermission, FolderAccessDto } from '../types';

interface FolderAccessDialogProps {
  folderId: string;
  folderName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

type ExpiryOption = 'permanent' | '1h' | '1d' | '1w' | '1m' | 'custom';

function computeExpiresAt(option: ExpiryOption, customDate: string): string | undefined {
  if (option === 'permanent') return undefined;
  if (option === 'custom') return customDate || undefined;

  const now = new Date();
  const offsets: Record<string, number> = {
    '1h': 60 * 60 * 1000,
    '1d': 24 * 60 * 60 * 1000,
    '1w': 7 * 24 * 60 * 60 * 1000,
    '1m': 30 * 24 * 60 * 60 * 1000,
  };
  return new Date(now.getTime() + offsets[option]!).toISOString();
}

export function FolderAccessDialog({ folderId, folderName, open, onOpenChange }: FolderAccessDialogProps) {
  const { t, i18n } = useTranslation('documents');
  const { data: accessList, isPending } = useFolderAccess(folderId);
  const grantAccess = useGrantFolderAccess(folderId);
  const revokeAccess = useRevokeFolderAccess(folderId);

  // Role dropdown (declared early — used in access list display below)
  const { data: roles } = useRoles();
  const allRoles = roles ?? [];

  // Resolve user/role names for the access list display
  const { data: accessUsersResult } = useUsers({ page: 1, pageSize: 50, search: undefined });
  const accessUsers = accessUsersResult?.items ?? [];

  // User search
  const [userSearch, setUserSearch] = useState('');
  const [selectedUser, setSelectedUser] = useState<{ id: string; firstName: string; lastName: string; email: string } | null>(null);
  const { data: usersResult, isPending: isUserSearchPending } = useUsers({ page: 1, pageSize: 10, search: userSearch || undefined });
  const users = usersResult?.items ?? [];
  const [selectedRoleId, setSelectedRoleId] = useState<string | undefined>(undefined);

  // Permission & expiry
  const [permission, setPermission] = useState<AccessPermission>('View');
  const [expiryOption, setExpiryOption] = useState<ExpiryOption>('permanent');
  const [customDate, setCustomDate] = useState('');

  // Revoke confirm
  const [revokeConfirmId, setRevokeConfirmId] = useState<string | null>(null);

  const resetForm = () => {
    setUserSearch('');
    setSelectedUser(null);
    setSelectedRoleId(undefined);
    setPermission('View');
    setExpiryOption('permanent');
    setCustomDate('');
  };

  useEffect(() => {
    if (!open) resetForm();
  }, [open]);

  const handleGrant = () => {
    const expiresAt = computeExpiresAt(expiryOption, customDate);
    grantAccess.mutate(
      {
        userId: selectedUser?.id,
        roleId: selectedRoleId,
        permission,
        expiresAt,
      },
      { onSuccess: () => resetForm() },
    );
  };

  const canGrant = (selectedUser || selectedRoleId) && !(selectedUser && selectedRoleId);

  const expiryOptions: { value: ExpiryOption; label: string }[] = useMemo(() => [
    { value: 'permanent', label: t('lockey_documents_folder_access_expiry_permanent') },
    { value: '1h', label: t('lockey_documents_folder_access_expiry_1h') },
    { value: '1d', label: t('lockey_documents_folder_access_expiry_1d') },
    { value: '1w', label: t('lockey_documents_folder_access_expiry_1w') },
    { value: '1m', label: t('lockey_documents_folder_access_expiry_1m') },
    { value: 'custom', label: t('lockey_documents_folder_access_expiry_custom') },
  ], [t]);

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{t('lockey_documents_folder_access_title')}</DialogTitle>
            <DialogDescription>
              {t('lockey_documents_folder_access_description', { folderName })}
            </DialogDescription>
          </DialogHeader>

          {/* Existing access list */}
          <div className="space-y-3">
            <h3 className="text-sm font-semibold">{t('lockey_documents_folder_access_current')}</h3>
            {isPending ? (
              <p className="text-sm text-muted-foreground">{t('lockey_common_loading', { ns: 'common' })}</p>
            ) : accessList && accessList.length > 0 ? (
              <div className="rounded-lg border">
                <table className="w-full text-sm" aria-label={t('lockey_documents_folder_access_title')}>
                  <thead>
                    <tr className="border-b bg-muted/50">
                      <th className="px-3 py-2 text-start">{t('lockey_documents_access_col_user')}</th>
                      <th className="px-3 py-2 text-start">{t('lockey_documents_access_col_role')}</th>
                      <th className="px-3 py-2 text-start">{t('lockey_documents_access_col_permission')}</th>
                      <th className="px-3 py-2 text-start">{t('lockey_documents_folder_access_col_expires')}</th>
                      <th className="px-3 py-2 text-start">{t('lockey_documents_col_actions')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {accessList.map((access: FolderAccessDto) => (
                      <tr key={access.id} className="border-b last:border-0">
                        <td className="px-3 py-2">
                          {access.userId
                            ? (() => {
                                const user = accessUsers.find((u) => u.id === access.userId);
                                return user
                                  ? `${user.firstName} ${user.lastName} (${user.email})`
                                  : access.userId;
                              })()
                            : '-'}
                        </td>
                        <td className="px-3 py-2">
                          {access.roleId
                            ? allRoles.find((r) => r.id === access.roleId)?.name ?? access.roleId
                            : '-'}
                        </td>
                        <td className="px-3 py-2">
                          <Badge variant="outline">{access.permission}</Badge>
                        </td>
                        <td className="px-3 py-2">
                          {access.expiresAt ? (
                            <span className={access.isExpired ? 'text-destructive' : ''}>
                              {new Date(access.expiresAt).toLocaleString(i18n.language)}
                              {access.isExpired && (
                                <Badge variant="destructive" className="ml-1">
                                  {t('lockey_documents_folder_access_expired')}
                                </Badge>
                              )}
                            </span>
                          ) : (
                            t('lockey_documents_folder_access_expiry_permanent')
                          )}
                        </td>
                        <td className="px-3 py-2">
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            onClick={() => setRevokeConfirmId(access.id)}
                          >
                            {t('lockey_documents_access_revoke')}
                          </Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">{t('lockey_documents_folder_access_empty')}</p>
            )}
          </div>

          {/* Grant form */}
          <div className="space-y-3 border-t pt-4">
            <h3 className="text-sm font-semibold">{t('lockey_documents_folder_access_grant')}</h3>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {/* User search combobox */}
              <SearchableDropdown
                value={selectedUser}
                onSelect={(user) => {
                  setSelectedUser(user);
                  setUserSearch('');
                  setSelectedRoleId(undefined);
                }}
                items={users}
                isLoading={isUserSearchPending}
                searchValue={userSearch}
                onSearchChange={(v) => {
                  setUserSearch(v);
                  setSelectedUser(null);
                  setSelectedRoleId(undefined);
                }}
                renderItem={(user) => <span>{user.firstName} {user.lastName} <span className="text-muted-foreground">({user.email})</span></span>}
                renderSelected={(user) => `${user.firstName} ${user.lastName} (${user.email})`}
                keyExtractor={(user) => user.id}
                placeholder={t('lockey_documents_folder_access_search_user')}
                label={t('lockey_documents_access_col_user')}
                className={selectedRoleId ? 'pointer-events-none opacity-50' : ''}
              />

              {/* Role dropdown */}
              <div>
                <label className="text-sm font-medium">{t('lockey_documents_access_col_role')}</label>
                <Select
                  value={selectedRoleId ?? ''}
                  onValueChange={(v) => {
                    setSelectedRoleId(v || undefined);
                    if (v) {
                      setSelectedUser(null);
                      setUserSearch('');
                    }
                  }}
                  disabled={!!selectedUser}
                >
                  <SelectTrigger className="mt-1" aria-label={t('lockey_documents_access_col_role')}>
                    <SelectValue placeholder={t('lockey_documents_folder_access_select_role')} />
                  </SelectTrigger>
                  <SelectContent>
                    {roles?.map((role) => (
                      <SelectItem key={role.id} value={role.id}>
                        {role.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              {/* Permission */}
              <div>
                <label className="text-xs font-medium">{t('lockey_documents_access_form_permission')}</label>
                <Select value={permission} onValueChange={(v) => setPermission(v as AccessPermission)}>
                  <SelectTrigger className="mt-1" aria-label={t('lockey_documents_access_form_permission')}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="View">{t('lockey_documents_access_permission_view')}</SelectItem>
                    <SelectItem value="Edit">{t('lockey_documents_access_permission_edit')}</SelectItem>
                    <SelectItem value="Manage">{t('lockey_documents_access_permission_manage')}</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              {/* Expiry */}
              <div>
                <label className="text-xs font-medium">{t('lockey_documents_folder_access_expiry')}</label>
                <Select value={expiryOption} onValueChange={(v) => setExpiryOption(v as ExpiryOption)}>
                  <SelectTrigger className="mt-1" aria-label={t('lockey_documents_folder_access_expiry')}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {expiryOptions.map((opt) => (
                      <SelectItem key={opt.value} value={opt.value}>
                        {opt.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Custom date */}
              {expiryOption === 'custom' && (
                <div>
                  <label className="text-xs font-medium">{t('lockey_documents_folder_access_expiry_date')}</label>
                  <Input
                    type="datetime-local"
                    value={customDate}
                    onChange={(e) => setCustomDate(e.target.value)}
                    className="mt-1"
                  />
                </div>
              )}
            </div>

            <DialogFooter>
              <Button
                type="button"
                onClick={handleGrant}
                disabled={!canGrant || grantAccess.isPending}
              >
                {t('lockey_documents_folder_access_grant')}
              </Button>
            </DialogFooter>
          </div>
        </DialogContent>
      </Dialog>

      {/* Revoke Confirm */}
      <ConfirmDialog
        open={revokeConfirmId !== null}
        onOpenChange={() => setRevokeConfirmId(null)}
        title={t('lockey_documents_folder_access_confirm_revoke_title')}
        description={t('lockey_documents_folder_access_confirm_revoke')}
        variant="destructive"
        onConfirm={() => {
          if (revokeConfirmId) {
            revokeAccess.mutate(revokeConfirmId, {
              onSuccess: () => setRevokeConfirmId(null),
            });
          }
        }}
        isPending={revokeAccess.isPending}
      />
    </>
  );
}
