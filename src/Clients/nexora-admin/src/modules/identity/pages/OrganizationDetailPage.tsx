import { useEffect, useMemo, useState } from 'react';
import { useParams, useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Trash2 } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { TabContentSkeleton } from '@/shared/components/feedback/TabContentSkeleton';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { DataTable, type ColumnDef } from '@/shared/components/data/DataTable';
import { usePagination } from '@/shared/hooks/usePagination';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { cn } from '@/shared/lib/utils';
import { useApiError } from '@/shared/hooks/useApiError';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/components/ui/select';
import { SUPPORTED_LOCALES, SUPPORTED_TIMEZONES, SUPPORTED_CURRENCIES, SUPPORTED_LANGUAGES } from '@/shared/lib/localeConstants';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import {
  useOrganization,
  useUpdateOrganization,
  useDeleteOrganization,
  useOrganizationMembers,
  useAddMember,
  useRemoveMember,
} from '../hooks/useOrganizations';
import { formatRelativeTime } from '@/shared/lib/date';
import { useUsers } from '../hooks/useUsers';
import type { OrganizationMemberDto, UpdateOrganizationRequest } from '../types';

function updateOrgSchemaFactory(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    name: z.string().min(1, { message: t('lockey_identity_validation_org_name_required') }).max(200, { message: t('lockey_identity_validation_org_name_max') }),
    timezone: z.string().refine((v) => (SUPPORTED_TIMEZONES as readonly string[]).includes(v), { message: t('lockey_identity_validation_org_timezone_required') }),
    defaultCurrency: z.string().refine((v) => (SUPPORTED_CURRENCIES as readonly string[]).includes(v), { message: t('lockey_identity_validation_org_currency_required') }),
    defaultLanguage: z.string().refine((v) => (SUPPORTED_LANGUAGES as readonly string[]).includes(v), { message: t('lockey_identity_validation_org_language_required') }),
    defaultLocale: z.string().refine((v) => (SUPPORTED_LOCALES as readonly string[]).includes(v), { message: t('lockey_identity_validation_org_locale_required') }),
  });
}

type TabKey = 'details' | 'members';

export default function OrganizationDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t } = useTranslation('identity');
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<TabKey>('details');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();
  const { page, pageSize, setPage } = usePagination();

  const { data: org, isPending } = useOrganization(id);
  const updateOrg = useUpdateOrganization(id);
  const deleteOrg = useDeleteOrganization();
  const { data: membersData, isPending: membersLoading } = useOrganizationMembers(id, { page, pageSize });
  const addMember = useAddMember(id);
  const removeMember = useRemoveMember(id);

  const [isEditing, setIsEditing] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [memberToRemove, setMemberToRemove] = useState<string | null>(null);
  const [addMemberOpen, setAddMemberOpen] = useState(false);

  const updateOrgSchema = useMemo(() => updateOrgSchemaFactory(t), [t]);

  const form = useForm<UpdateOrganizationRequest>({
    resolver: zodResolver(updateOrgSchema),
  });

  const { isBlocked: isEditBlocked, proceed: proceedEdit, reset: resetEdit } =
    useUnsavedChangesGuard(isEditing && form.formState.isDirty);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_organizations', path: '/identity/organizations' },
      { label: org?.name ?? '...' },
    ]);
  }, [setBreadcrumbs, org]);

  useEffect(() => {
    if (org && isEditing) {
      form.reset({
        name: org.name,
        timezone: org.timezone,
        defaultCurrency: org.defaultCurrency,
        defaultLanguage: org.defaultLanguage,
        defaultLocale: org.defaultLocale,
      });
    }
  }, [org, isEditing, form]);

  function handleTabChange(tab: TabKey) {
    setActiveTab(tab);
    window.scrollTo(0, 0);
  }

  if (isPending) return <LoadingSkeleton lines={8} />;
  if (!org) {
    return (
      <div className="flex min-h-[200px] flex-col items-center justify-center gap-4 p-8">
        <p className="text-muted-foreground">
          {t('lockey_error_not_found', { ns: 'error' })}
        </p>
        <Button type="button" variant="outline" onClick={() => navigate('/identity/organizations')}>
          {t('lockey_common_back', { ns: 'common' })}
        </Button>
      </div>
    );
  }

  const memberColumns: ColumnDef<OrganizationMemberDto>[] = [
    {
      key: 'name',
      header: t('lockey_identity_col_name'),
      render: (row) => `${row.firstName} ${row.lastName}`,
    },
    { key: 'email', header: t('lockey_identity_col_email'), render: (row) => row.email },
    {
      key: 'joinedAt',
      header: t('lockey_identity_col_joined_at'),
      render: (row) => formatRelativeTime(row.joinedAt),
    },
    {
      key: 'actions',
      header: t('lockey_identity_col_actions'),
      render: (row) =>
        hasPermission('identity.organization.remove-member') ? (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={() => setMemberToRemove(row.userId)}
            title={t('lockey_identity_action_remove_member')}
          >
            <Trash2 className="h-4 w-4 text-destructive" />
          </Button>
        ) : null,
    },
  ];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{org.name}</h1>
          <div className="flex items-center gap-2 mt-1">
            <span className="text-sm text-muted-foreground">{org.slug}</span>
          </div>
        </div>
        <div className="flex gap-2">
          {hasPermission('identity.organization.delete') && (
            <Button
              type="button"
              variant="destructive"
              onClick={() => setShowDeleteConfirm(true)}
            >
              {t('lockey_identity_action_delete')}
            </Button>
          )}
          {hasPermission('identity.organization.update') && (
            <Button
              type="button"
              variant={isEditing ? 'outline' : 'default'}
              onClick={() => setIsEditing(!isEditing)}
            >
              {isEditing ? t('lockey_common_cancel', { ns: 'common' }) : t('lockey_identity_action_edit')}
            </Button>
          )}
        </div>
      </div>

      {/* Tab navigation */}
      <div className="flex gap-1 border-b">
        {([
          { key: 'details' as const, label: t('lockey_identity_tab_details') },
          { key: 'members' as const, label: t('lockey_identity_tab_members') },
        ]).map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => handleTabChange(tab.key)}
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
        <div className="mt-4">
          {isEditing ? (
            <form
              onSubmit={form.handleSubmit((data) => {
                updateOrg.mutate(data, {
                  onSuccess: () => setIsEditing(false),
                  onError: (err) => handleApiError(err, form.setError),
                });
              })}
              className="space-y-4"
            >
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <label htmlFor="orgName" className="text-sm font-medium">
                    {t('lockey_identity_form_org_name')}
                  </label>
                  <Input id="orgName" {...form.register('name')} />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('lockey_identity_form_org_locale')}</label>
                  <Select
                    value={form.watch('defaultLocale')}
                    onValueChange={(v) => form.setValue('defaultLocale', v, { shouldDirty: true })}
                  >
                    <SelectTrigger aria-label={t('lockey_identity_form_org_locale')}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {SUPPORTED_LOCALES.map((l) => (
                        <SelectItem key={l} value={l}>{l}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('lockey_identity_form_org_timezone')}</label>
                  <Select
                    value={form.watch('timezone')}
                    onValueChange={(v) => form.setValue('timezone', v, { shouldDirty: true })}
                  >
                    <SelectTrigger aria-label={t('lockey_identity_form_org_timezone')}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {SUPPORTED_TIMEZONES.map((tz) => (
                        <SelectItem key={tz} value={tz}>{tz}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('lockey_identity_form_org_currency')}</label>
                  <Select
                    value={form.watch('defaultCurrency')}
                    onValueChange={(v) => form.setValue('defaultCurrency', v, { shouldDirty: true })}
                  >
                    <SelectTrigger aria-label={t('lockey_identity_form_org_currency')}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {SUPPORTED_CURRENCIES.map((c) => (
                        <SelectItem key={c} value={c}>{c}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">{t('lockey_identity_form_org_language')}</label>
                  <Select
                    value={form.watch('defaultLanguage')}
                    onValueChange={(v) => form.setValue('defaultLanguage', v, { shouldDirty: true })}
                  >
                    <SelectTrigger aria-label={t('lockey_identity_form_org_language')}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {SUPPORTED_LANGUAGES.map((lang) => (
                        <SelectItem key={lang} value={lang}>{lang}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="flex justify-end gap-2">
                <Button type="submit" disabled={updateOrg.isPending}>
                  {updateOrg.isPending
                    ? t('lockey_common_loading', { ns: 'common' })
                    : t('lockey_common_save', { ns: 'common' })}
                </Button>
              </div>
            </form>
          ) : (
            <dl className="grid gap-3 sm:grid-cols-2">
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_locale')}</dt>
                <dd>{org.defaultLocale}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_timezone')}</dt>
                <dd>{org.timezone}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_currency')}</dt>
                <dd>{org.defaultCurrency}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_language')}</dt>
                <dd>{org.defaultLanguage}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_member_count')}</dt>
                <dd>{org.memberCount}</dd>
              </div>
            </dl>
          )}
        </div>
      )}

      {activeTab === 'members' && (
        <div className="mt-4 space-y-4">
          {hasPermission('identity.organization.add-member') && (
            <div className="flex justify-end">
              <Button size="sm" onClick={() => setAddMemberOpen(true)}>
                {t('lockey_identity_action_add_member')}
              </Button>
            </div>
          )}
          {membersLoading ? (
            <TabContentSkeleton />
          ) : (
            <DataTable
              columns={memberColumns}
              data={membersData?.items ?? []}
              totalCount={membersData?.totalCount ?? 0}
              page={page}
              pageSize={pageSize}
              onPageChange={setPage}
              isLoading={membersLoading}
              emptyMessage={t('lockey_identity_empty_members')}
            />
          )}
        </div>
      )}

      <ConfirmDialog
        open={showDeleteConfirm}
        onOpenChange={setShowDeleteConfirm}
        title={t('lockey_identity_action_delete')}
        description={t('lockey_identity_confirm_delete_org')}
        variant="destructive"
        onConfirm={() => {
          deleteOrg.mutate(id, {
            onSuccess: () => void navigate('/identity/organizations'),
          });
        }}
        isPending={deleteOrg.isPending}
      />

      <ConfirmDialog
        open={memberToRemove !== null}
        onOpenChange={() => setMemberToRemove(null)}
        title={t('lockey_identity_action_remove_member')}
        description={t('lockey_identity_confirm_remove_member')}
        variant="destructive"
        onConfirm={() => {
          if (memberToRemove) {
            removeMember.mutate(memberToRemove, {
              onSuccess: () => setMemberToRemove(null),
              onError: () => setMemberToRemove(null),
            });
          }
        }}
        isPending={removeMember.isPending}
      />

      <AddMemberDialog
        open={addMemberOpen}
        onOpenChange={setAddMemberOpen}
        onAdd={(userId) => {
          addMember.mutate({ userId }, {
            onSuccess: () => setAddMemberOpen(false),
          });
        }}
        isPending={addMember.isPending}
        existingMemberIds={membersData?.items.map((m) => m.userId) ?? []}
      />

      <ConfirmDialog
        open={isEditBlocked}
        onOpenChange={(open) => { if (!open) resetEdit(); }}
        title={t('lockey_common_unsaved_changes_title', { ns: 'common' })}
        description={t('lockey_common_unsaved_changes_description', { ns: 'common' })}
        onConfirm={proceedEdit}
        confirmLabel={t('lockey_common_leave', { ns: 'common' })}
      />
    </div>
  );
}

function AddMemberDialog({
  open,
  onOpenChange,
  onAdd,
  isPending,
  existingMemberIds,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onAdd: (userId: string) => void;
  isPending: boolean;
  existingMemberIds: string[];
}) {
  const { t } = useTranslation('identity');
  const [search, setSearch] = useState('');
  const { data: usersData } = useUsers({ page: 1, pageSize: 50 });

  const availableUsers = usersData?.items.filter(
    (u) => !existingMemberIds.includes(u.id),
  ) ?? [];

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
          <DialogTitle>{t('lockey_identity_action_add_member')}</DialogTitle>
          <DialogDescription className="sr-only">{t('lockey_identity_action_add_member')}</DialogDescription>
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
                disabled={isPending}
                onClick={() => onAdd(user.id)}
                className="flex w-full items-center justify-between rounded-md px-3 py-2 text-sm hover:bg-accent transition-colors"
              >
                <div>
                  <p className="font-medium">{user.firstName} {user.lastName}</p>
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
