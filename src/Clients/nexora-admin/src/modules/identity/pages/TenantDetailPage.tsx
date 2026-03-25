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
import { usePermissions } from '@/shared/hooks/usePermissions';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { useTenant, useUpdateTenantStatus } from '../hooks/useTenants';
import { useTenantModules, useInstallModule, useActivateModule, useDeactivateModule, useUninstallModule } from '../hooks/useModuleManagement';
import { TenantStatusBadge } from '../components/UserStatusBadge';

export default function TenantDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('identity');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();

  const { data: tenant, isPending } = useTenant(id);
  const updateStatus = useUpdateTenantStatus(id);
  const { data: modules } = useTenantModules(id);
  const installModule = useInstallModule(id);
  const activateModule = useActivateModule(id);
  const deactivateModule = useDeactivateModule(id);
  const uninstallModule = useUninstallModule(id);

  const [confirmAction, setConfirmAction] = useState<'suspend' | 'terminate' | null>(null);
  const [moduleToUninstall, setModuleToUninstall] = useState<string | null>(null);
  const [installOpen, setInstallOpen] = useState(false);

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
          {hasPermission('identity.tenants.update') && canActivate && (
            <Button type="button" onClick={() => updateStatus.activate()}>
              {t('lockey_identity_action_activate')}
            </Button>
          )}
          {hasPermission('identity.tenants.update') && canSuspend && (
            <Button
              type="button"
              variant="outline"
              onClick={() => setConfirmAction('suspend')}
            >
              {t('lockey_identity_action_suspend')}
            </Button>
          )}
          {hasPermission('identity.tenants.update') && canTerminate && (
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
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>{t('lockey_identity_tenant_modules_title')}</CardTitle>
            {hasPermission('identity.modules.manage') && (
              <Button size="sm" onClick={() => setInstallOpen(true)}>
                {t('lockey_identity_action_install_module')}
              </Button>
            )}
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
                      <span className="font-medium">{t('lockey_common_module_' + mod.moduleName, { ns: 'common', defaultValue: mod.moduleName })}</span>
                      <Badge variant={mod.isActive ? 'default' : 'secondary'}>
                        {mod.isActive ? t('lockey_identity_status_active') : t('lockey_identity_status_inactive')}
                      </Badge>
                    </div>
                    <div className="flex items-center gap-1">
                      {mod.isActive ? (
                        <>
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            disabled={deactivateModule.isPending}
                            onClick={() => {
                              deactivateModule.mutate(mod.moduleName, {
                                onError: (err) => handleApiError(err),
                              });
                            }}
                          >
                            {t('lockey_identity_action_deactivate_module')}
                          </Button>
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            className="text-destructive"
                            onClick={() => setModuleToUninstall(mod.moduleName)}
                          >
                            {t('lockey_identity_action_uninstall')}
                          </Button>
                        </>
                      ) : (
                        <>
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            disabled={activateModule.isPending}
                            onClick={() => {
                              activateModule.mutate(mod.moduleName, {
                                onError: (err) => handleApiError(err),
                              });
                            }}
                          >
                            {t('lockey_identity_action_activate_module')}
                          </Button>
                          <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            className="text-destructive"
                            onClick={() => setModuleToUninstall(mod.moduleName)}
                          >
                            {t('lockey_identity_action_uninstall')}
                          </Button>
                        </>
                      )}
                    </div>
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
        description={t('lockey_identity_confirm_uninstall_module_permanent')}
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

      <InstallModuleDialog
        open={installOpen}
        onOpenChange={setInstallOpen}
        installedModules={modules?.map((m) => m.moduleName) ?? []}
        onInstall={(moduleName) => {
          installModule.mutate(moduleName, {
            onSuccess: () => setInstallOpen(false),
            onError: (err) => {
              handleApiError(err);
            },
          });
        }}
        isPending={installModule.isPending}
      />
    </div>
  );
}

const AVAILABLE_MODULES = [
  'identity', 'contacts', 'documents', 'notifications', 'reporting',
  'crm', 'donations', 'sponsorship', 'events',
] as const;

function InstallModuleDialog({
  open,
  onOpenChange,
  installedModules,
  onInstall,
  isPending,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  installedModules: string[];
  onInstall: (moduleName: string) => void;
  isPending: boolean;
}) {
  const { t } = useTranslation('identity');
  const activeModules = new Set(installedModules);
  const available = AVAILABLE_MODULES.filter((m) => !activeModules.has(m));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{t('lockey_identity_action_install_module')}</DialogTitle>
          <DialogDescription className="sr-only">{t('lockey_identity_action_install_module')}</DialogDescription>
        </DialogHeader>
        <div className="space-y-1">
          {available.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4 text-center">
              {t('lockey_identity_all_modules_installed')}
            </p>
          ) : (
            available.map((moduleName) => (
              <button
                key={moduleName}
                type="button"
                disabled={isPending}
                onClick={() => onInstall(moduleName)}
                className="flex w-full items-center justify-between rounded-md px-3 py-2 text-sm hover:bg-accent transition-colors capitalize"
              >
                {t('lockey_common_module_' + moduleName, { ns: 'common', defaultValue: moduleName })}
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
