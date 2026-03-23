import { useEffect, useState } from 'react';
import { useParams } from 'react-router';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useTenant, useUpdateTenantStatus } from '../hooks/useTenants';
import { useTenantModules, useUninstallModule } from '../hooks/useModuleManagement';
import { TenantStatusBadge } from '../components/UserStatusBadge';

export default function TenantDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('identity');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();

  const { data: tenant, isPending } = useTenant(id);
  const updateStatus = useUpdateTenantStatus(id);
  const { data: modules } = useTenantModules(id);
  const uninstallModule = useUninstallModule(id);

  const [confirmAction, setConfirmAction] = useState<'suspend' | 'terminate' | null>(null);
  const [moduleToUninstall, setModuleToUninstall] = useState<string | null>(null);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_identity_module_name' },
      { label: 'lockey_identity_nav_tenants', path: '/identity/tenants' },
      { label: tenant?.name ?? '...' },
    ]);
  }, [setBreadcrumbs, tenant]);

  if (isPending) return <LoadingSkeleton lines={8} />;
  if (!tenant) return null;

  const canActivate = tenant.status === 'Trial' || tenant.status === 'Suspended';
  const canSuspend = tenant.status === 'Active';
  const canTerminate = tenant.status !== 'Terminated';

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{tenant.name}</h1>
          <p className="text-sm text-muted-foreground">{tenant.slug}</p>
        </div>
        <div className="flex gap-2">
          {canActivate && (
            <Button type="button" onClick={() => updateStatus.activate()}>
              {t('lockey_identity_action_activate')}
            </Button>
          )}
          {canSuspend && (
            <Button
              type="button"
              variant="outline"
              onClick={() => setConfirmAction('suspend')}
            >
              {t('lockey_identity_action_suspend')}
            </Button>
          )}
          {canTerminate && (
            <Button
              type="button"
              variant="destructive"
              onClick={() => setConfirmAction('terminate')}
            >
              {t('lockey_identity_action_terminate')}
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_identity_tenant_detail_title')}</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="space-y-3">
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_status')}</dt>
                <dd><TenantStatusBadge status={tenant.status} /></dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_realm')}</dt>
                <dd>{tenant.realmId ?? '—'}</dd>
              </div>
              <div>
                <dt className="text-sm text-muted-foreground">{t('lockey_identity_col_created_at')}</dt>
                <dd>{new Date(tenant.createdAt).toLocaleDateString(i18n.language)}</dd>
              </div>
            </dl>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('lockey_identity_tenant_modules_title')}</CardTitle>
          </CardHeader>
          <CardContent>
            {!modules?.length ? (
              <p className="text-sm text-muted-foreground">
                {t('lockey_identity_empty_modules')}
              </p>
            ) : (
              <ul className="space-y-2">
                {modules.map((mod) => (
                  <li key={mod.id} className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span className="font-medium">{mod.moduleName}</span>
                      <Badge variant={mod.isActive ? 'default' : 'secondary'}>
                        {mod.isActive ? t('lockey_identity_status_active') : t('lockey_identity_status_inactive')}
                      </Badge>
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => setModuleToUninstall(mod.moduleName)}
                    >
                      {t('lockey_identity_action_uninstall')}
                    </Button>
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
          confirmAction === 'suspend'
            ? t('lockey_identity_action_suspend')
            : t('lockey_identity_action_terminate')
        }
        description={
          confirmAction === 'suspend'
            ? t('lockey_identity_confirm_suspend_tenant')
            : t('lockey_identity_confirm_terminate_tenant')
        }
        variant="destructive"
        onConfirm={() => {
          const action = confirmAction === 'suspend' ? 'suspend' : 'terminate';
          updateStatus.mutate({ action }, {
            onSuccess: () => setConfirmAction(null),
            onError: (err) => {
              setConfirmAction(null);
              handleApiError(err);
            },
          });
        }}
        isPending={updateStatus.isPending}
      />

      <ConfirmDialog
        open={moduleToUninstall !== null}
        onOpenChange={() => setModuleToUninstall(null)}
        title={t('lockey_identity_action_uninstall')}
        description={t('lockey_identity_confirm_uninstall_module')}
        variant="destructive"
        onConfirm={() => {
          if (moduleToUninstall) {
            uninstallModule.mutate(moduleToUninstall, {
              onSuccess: () => setModuleToUninstall(null),
              onError: (err) => {
                setModuleToUninstall(null);
                handleApiError(err);
              },
            });
          }
        }}
        isPending={uninstallModule.isPending}
      />
    </div>
  );
}
