import { useEffect, useState } from 'react';
import { useParams } from 'react-router';
import { useTranslation } from 'react-i18next';

import { Blocks } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { TabContentSkeleton } from '@/shared/components/feedback/TabContentSkeleton';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { cn } from '@/shared/lib/utils';
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/components/ui/select';
import { SUPPORTED_LOCALES, SUPPORTED_CURRENCIES, SUPPORTED_TIMEZONES, SUPPORTED_LANGUAGES } from '@/shared/lib/localeConstants';
import { useTenant, useUpdateTenantSettings, useUpdateTenantStatus } from '../hooks/useTenants';
import { useTenantModules, useInstallModule, useActivateModule, useDeactivateModule, useUninstallModule, useRegisteredModules } from '../hooks/useModuleManagement';
import type { RegisteredModuleDto } from '../hooks/useModuleManagement';
import { TenantStatusBadge } from '../components/UserStatusBadge';

type TabKey = 'details' | 'modules' | 'settings';

export default function TenantDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('identity');
  const [activeTab, setActiveTab] = useState<TabKey>('details');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { handleApiError } = useApiError();
  const { hasPermission } = usePermissions();

  const { data: tenant, isPending } = useTenant(id);
  const updateStatus = useUpdateTenantStatus(id);
  const updateSettings = useUpdateTenantSettings(id);

  const [settingsLocale, setSettingsLocale] = useState('');
  const [settingsCurrency, setSettingsCurrency] = useState('');
  const [settingsTimezone, setSettingsTimezone] = useState('');
  const [settingsDocLang, setSettingsDocLang] = useState('');
  const { data: modules, isPending: isModulesPending } = useTenantModules(id);
  const { data: registeredModules } = useRegisteredModules();
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

  useEffect(() => {
    if (tenant) {
      setSettingsLocale(tenant.defaultLocale);
      setSettingsCurrency(tenant.defaultCurrency);
      setSettingsTimezone(tenant.defaultTimezone);
      setSettingsDocLang(tenant.defaultDocumentLanguage);
    }
  }, [tenant]);

  function handleTabChange(tab: TabKey) {
    setActiveTab(tab);
    window.scrollTo(0, 0);
  }

  if (isPending) return <LoadingSkeleton lines={8} />;
  if (!tenant) return null;

  const canActivate = tenant.status === 'Trial' || tenant.status === 'Suspended';
  const canSuspend = tenant.status === 'Active';
  const canTerminate = tenant.status !== 'Terminated';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{tenant.name}</h1>
          <div className="flex items-center gap-2 mt-1">
            <TenantStatusBadge status={tenant.status} />
            <span className="text-sm text-muted-foreground">{tenant.slug}</span>
          </div>
        </div>
        <div className="flex gap-2">
          {hasPermission('identity.tenants.manage') && canActivate && (
            <Button type="button" onClick={() => updateStatus.activate()}>
              {t('lockey_identity_action_activate')}
            </Button>
          )}
          {hasPermission('identity.tenants.manage') && canSuspend && (
            <Button
              type="button"
              variant="outline"
              onClick={() => setConfirmAction('suspend')}
            >
              {t('lockey_identity_action_suspend')}
            </Button>
          )}
          {hasPermission('identity.tenants.manage') && canTerminate && (
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

      {/* Tab navigation */}
      <div className="flex gap-1 border-b">
        {([
          { key: 'details' as const, label: t('lockey_identity_tab_details') },
          { key: 'modules' as const, label: t('lockey_identity_tab_modules') },
          { key: 'settings' as const, label: t('lockey_identity_tab_settings') },
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
        </div>
      )}

      {activeTab === 'modules' && (
        <div className="mt-4 space-y-4">
          {hasPermission('identity.modules.manage') && (
            <div className="flex justify-end">
              <Button size="sm" onClick={() => setInstallOpen(true)}>
                {t('lockey_identity_action_install_module')}
              </Button>
            </div>
          )}
          {isModulesPending ? (
            <TabContentSkeleton />
          ) : !modules?.length ? (
            <EmptyState
              icon={Blocks}
              title={t('lockey_identity_empty_modules')}
              description={t('lockey_identity_empty_modules_description')}
              action={
                hasPermission('identity.modules.manage')
                  ? { label: t('lockey_identity_action_install_module'), onClick: () => setInstallOpen(true) }
                  : undefined
              }
            />
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
                            deactivateModule.mutate(mod.moduleName);
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
                            activateModule.mutate(mod.moduleName);
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
        </div>
      )}

      {activeTab === 'settings' && (
        <div className="mt-4 max-w-md space-y-4">
          <div className="space-y-2">
            <label className="text-sm font-medium">{t('lockey_identity_form_tenant_locale')}</label>
            <Select value={settingsLocale} onValueChange={setSettingsLocale}>
              <SelectTrigger aria-label={t('lockey_identity_form_tenant_locale')}>
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
            <label className="text-sm font-medium">{t('lockey_identity_form_tenant_currency')}</label>
            <Select value={settingsCurrency} onValueChange={setSettingsCurrency}>
              <SelectTrigger aria-label={t('lockey_identity_form_tenant_currency')}>
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
            <label className="text-sm font-medium">{t('lockey_identity_form_tenant_timezone')}</label>
            <Select value={settingsTimezone} onValueChange={setSettingsTimezone}>
              <SelectTrigger aria-label={t('lockey_identity_form_tenant_timezone')}>
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
            <label className="text-sm font-medium">{t('lockey_identity_form_tenant_doc_language')}</label>
            <Select value={settingsDocLang} onValueChange={setSettingsDocLang}>
              <SelectTrigger aria-label={t('lockey_identity_form_tenant_doc_language')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {SUPPORTED_LANGUAGES.map((l) => (
                  <SelectItem key={l} value={l}>{l}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {hasPermission('identity.tenants.manage') && (
            <Button
              type="button"
              disabled={updateSettings.isPending}
              onClick={() => {
                updateSettings.mutate({
                  defaultLocale: settingsLocale,
                  defaultCurrency: settingsCurrency,
                  defaultTimezone: settingsTimezone,
                  defaultDocumentLanguage: settingsDocLang,
                });
              }}
            >
              {t('lockey_identity_action_save_settings')}
            </Button>
          )}
        </div>
      )}

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
        registeredModules={registeredModules ?? []}
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

function InstallModuleDialog({
  open,
  onOpenChange,
  installedModules,
  registeredModules,
  onInstall,
  isPending,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  installedModules: string[];
  registeredModules: RegisteredModuleDto[];
  onInstall: (moduleName: string) => void;
  isPending: boolean;
}) {
  const { t } = useTranslation('identity');
  const activeModules = new Set(installedModules);
  const available = registeredModules.filter((m) => !activeModules.has(m.name));

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
            available.map((mod) => (
              <button
                key={mod.name}
                type="button"
                disabled={isPending}
                onClick={() => onInstall(mod.name)}
                className="flex w-full items-center justify-between rounded-md px-3 py-2 text-sm hover:bg-accent transition-colors"
              >
                <span className="font-medium capitalize">
                  {t('lockey_common_module_' + mod.name, { ns: 'common', defaultValue: mod.name })}
                </span>
                <span className="text-xs text-muted-foreground">v{mod.version}</span>
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
