import { useEffect, useState } from 'react';
import { useParams } from 'react-router';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { Badge } from '@/shared/components/ui/badge';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useUser, useUpdateProfile, useUpdateUserStatus } from '../hooks/useUsers';
import { UserStatusBadge } from '../components/UserStatusBadge';
import { UserForm } from '../components/UserForm';

export default function UserDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('identity');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const { data: user, isPending } = useUser(id);
  const updateProfile = useUpdateProfile(id);
  const updateStatus = useUpdateUserStatus(id);

  const [isEditing, setIsEditing] = useState(false);
  const [confirmAction, setConfirmAction] = useState<'activate' | 'deactivate' | null>(null);

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
          <Button
            type="button"
            variant={isEditing ? 'outline' : 'default'}
            onClick={() => setIsEditing(!isEditing)}
          >
            {isEditing ? t('lockey_common_cancel', { ns: 'common' }) : t('lockey_identity_action_edit')}
          </Button>
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
          <CardHeader>
            <CardTitle>{t('lockey_identity_tab_organizations')}</CardTitle>
          </CardHeader>
          <CardContent>
            {user.organizations.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {t('lockey_identity_empty_orgs')}
              </p>
            ) : (
              <ul className="space-y-2">
                {user.organizations.map((org) => (
                  <li key={org.organizationId} className="flex items-center gap-2">
                    <span>{org.organizationName}</span>
                    {org.isDefault && (
                      <Badge variant="secondary">{t('lockey_common_dashboard', { ns: 'common' })}</Badge>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      <ConfirmDialog
        open={confirmAction !== null}
        onOpenChange={() => setConfirmAction(null)}
        title={
          confirmAction === 'deactivate'
            ? t('lockey_identity_action_deactivate')
            : t('lockey_identity_action_activate')
        }
        description={
          confirmAction === 'deactivate'
            ? t('lockey_identity_confirm_deactivate_user')
            : t('lockey_identity_action_activate')
        }
        variant={confirmAction === 'deactivate' ? 'destructive' : 'default'}
        onConfirm={() => {
          if (confirmAction === 'activate') {
            updateStatus.activate();
          } else {
            updateStatus.deactivate();
          }
          setConfirmAction(null);
        }}
        isPending={updateStatus.isPending}
      />
    </div>
  );
}
