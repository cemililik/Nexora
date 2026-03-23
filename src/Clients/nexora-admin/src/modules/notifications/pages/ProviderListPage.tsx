import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import { Badge } from '@/shared/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import {
  useProviders,
  useCreateProvider,
  useUpdateProvider,
  useTestProvider,
} from '../hooks/useProviders';
import { CHANNELS, CHANNEL_KEY_MAP, PROVIDER_NAMES, PROVIDER_NAME_KEY_MAP } from '../constants';
import type { NotificationProviderDto, NotificationChannel } from '../types';

function createProviderSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    channel: z.enum([...CHANNELS], { message: t('lockey_notifications_validation_channel_required') }),
    providerName: z.enum([...PROVIDER_NAMES], { message: t('lockey_notifications_validation_provider_name_required') }),
    config: z.string().min(1, t('lockey_notifications_validation_config_required')),
    dailyLimit: z.number().min(1, t('lockey_notifications_validation_daily_limit_min')),
    isDefault: z.boolean(),
  });
}

function createTestSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    testRecipient: z.string().min(1, t('lockey_notifications_validation_test_recipient_required')),
  });
}

type ProviderFormValues = z.infer<ReturnType<typeof createProviderSchema>>;
type TestFormValues = z.infer<ReturnType<typeof createTestSchema>>;

export default function ProviderListPage() {
  const { t, i18n } = useTranslation('notifications');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('notifications.provider.manage');

  const [searchParams, setSearchParams] = useSearchParams();
  const channelFilter = (searchParams.get('channel') as NotificationChannel) || undefined;

  const { data: providers, isPending } = useProviders(channelFilter);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingProvider, setEditingProvider] = useState<NotificationProviderDto | null>(null);
  const [testDialogOpen, setTestDialogOpen] = useState(false);
  const [testProviderId, setTestProviderId] = useState<string | null>(null);

  const createProvider = useCreateProvider();
  const updateProvider = useUpdateProvider(editingProvider?.id ?? '');
  const testProvider = useTestProvider();

  const providerSchema = createProviderSchema(t);
  const testSchema = createTestSchema(t);

  const form = useForm<ProviderFormValues>({
    resolver: zodResolver(providerSchema),
    defaultValues: {
      channel: 'Email',
      providerName: 'SendGrid',
      config: '',
      dailyLimit: 1000,
      isDefault: false,
    },
  });

  const testForm = useForm<TestFormValues>({
    resolver: zodResolver(testSchema),
    defaultValues: { testRecipient: '' },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_notifications_module_name' },
      { label: 'lockey_notifications_providers_title' },
    ]);
  }, [setBreadcrumbs]);

  const openCreateDialog = () => {
    setEditingProvider(null);
    form.reset({
      channel: 'Email',
      providerName: 'SendGrid',
      config: '',
      dailyLimit: 1000,
      isDefault: false,
    });
    setDialogOpen(true);
  };

  const openEditDialog = (provider: NotificationProviderDto) => {
    setEditingProvider(provider);
    form.reset({
      channel: provider.channel,
      providerName: provider.providerName,
      config: '',
      dailyLimit: provider.dailyLimit,
      isDefault: provider.isDefault,
    });
    setDialogOpen(true);
  };

  const onSubmit = (values: ProviderFormValues) => {
    if (editingProvider) {
      updateProvider.mutate(
        {
          config: values.config,
          dailyLimit: values.dailyLimit,
          isDefault: values.isDefault,
        },
        { onSuccess: () => { form.reset(); setDialogOpen(false); } },
      );
    } else {
      createProvider.mutate(
        {
          channel: values.channel,
          providerName: values.providerName,
          config: values.config,
          dailyLimit: values.dailyLimit,
          isDefault: values.isDefault,
        },
        { onSuccess: () => { form.reset(); setDialogOpen(false); } },
      );
    }
  };

  const onTest = (values: TestFormValues) => {
    if (testProviderId) {
      testProvider.mutate(
        { id: testProviderId, data: { testRecipient: values.testRecipient } },
        {
          onSuccess: () => {
            testForm.reset();
            setTestDialogOpen(false);
          },
        },
      );
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('lockey_notifications_providers_title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('lockey_notifications_providers_description')}
          </p>
        </div>
        {canManage && (
          <Button type="button" onClick={openCreateDialog}>
            {t('lockey_notifications_providers_create')}
          </Button>
        )}
      </div>

      <div className="flex items-center gap-4">
        <Select
          value={channelFilter ?? '__all__'}
          onValueChange={(v) => {
            setSearchParams((prev: URLSearchParams) => {
              const next = new URLSearchParams(prev);
              if (v === '__all__') {
                next.delete('channel');
              } else {
                next.set('channel', v);
              }
              return next;
            });
          }}
        >
          <SelectTrigger className="w-48" aria-label={t('lockey_notifications_aria_channel_filter')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t('lockey_notifications_providers_filter_all_channels')}</SelectItem>
            {CHANNELS.map((c) => (
              <SelectItem key={c} value={c}>
                {t(CHANNEL_KEY_MAP[c])}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isPending ? (
        <LoadingSkeleton />
      ) : providers && providers.length > 0 ? (
        <div className="rounded-lg border">
          <table className="w-full text-sm" aria-label={t('lockey_notifications_aria_providers_table')}>
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-4 py-2 text-start">{t('lockey_notifications_providers_col_provider')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_notifications_providers_col_channel')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_notifications_providers_col_default')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_notifications_providers_col_daily_limit')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_notifications_providers_col_sent_today')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_notifications_providers_col_created_at')}</th>
                <th className="px-4 py-2 text-start">{t('lockey_notifications_providers_col_actions')}</th>
              </tr>
            </thead>
            <tbody>
              {providers.map((provider) => (
                <tr key={provider.id} className="border-b last:border-0">
                  <td className="px-4 py-2 font-medium">
                    {t(PROVIDER_NAME_KEY_MAP[provider.providerName])}
                  </td>
                  <td className="px-4 py-2">{t(CHANNEL_KEY_MAP[provider.channel])}</td>
                  <td className="px-4 py-2">
                    {provider.isDefault && (
                      <Badge variant="default">{t('lockey_notifications_providers_col_default')}</Badge>
                    )}
                  </td>
                  <td className="px-4 py-2">{provider.dailyLimit}</td>
                  <td className="px-4 py-2">{provider.sentToday}</td>
                  <td className="px-4 py-2">
                    {new Date(provider.createdAt).toLocaleDateString(i18n.language)}
                  </td>
                  <td className="px-4 py-2">
                    {canManage && (
                      <div className="flex gap-1">
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => openEditDialog(provider)}
                        >
                          {t('lockey_notifications_providers_edit')}
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setTestProviderId(provider.id);
                            testForm.reset();
                            setTestDialogOpen(true);
                          }}
                        >
                          {t('lockey_notifications_providers_test')}
                        </Button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{t('lockey_notifications_providers_empty')}</p>
      )}

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {editingProvider
                ? t('lockey_notifications_providers_edit')
                : t('lockey_notifications_providers_create')}
            </DialogTitle>
          </DialogHeader>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            {!editingProvider && (
              <>
                <div>
                  <label htmlFor="provider-channel" className="text-sm font-medium">{t('lockey_notifications_providers_form_channel')}</label>
                  <Select
                    value={form.watch('channel')}
                    onValueChange={(v) => form.setValue('channel', v as typeof CHANNELS[number])}
                  >
                    <SelectTrigger id="provider-channel" className="mt-1">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {CHANNELS.map((c) => (
                        <SelectItem key={c} value={c}>
                          {t(CHANNEL_KEY_MAP[c])}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <label htmlFor="provider-name" className="text-sm font-medium">{t('lockey_notifications_providers_form_provider_name')}</label>
                  <Select
                    value={form.watch('providerName')}
                    onValueChange={(v) => form.setValue('providerName', v as typeof PROVIDER_NAMES[number])}
                  >
                    <SelectTrigger id="provider-name" className="mt-1">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {PROVIDER_NAMES.map((p) => (
                        <SelectItem key={p} value={p}>
                          {t(PROVIDER_NAME_KEY_MAP[p])}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </>
            )}
            <div>
              <label htmlFor="provider-config" className="text-sm font-medium">{t('lockey_notifications_providers_form_config')}</label>
              <textarea
                id="provider-config"
                {...form.register('config')}
                rows={4}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm font-mono"
                placeholder={t('lockey_notifications_providers_form_config_placeholder')}
              />
              {form.formState.errors.config?.message && (
                <p className="mt-1 text-sm text-destructive">{form.formState.errors.config.message}</p>
              )}
            </div>
            <div>
              <label htmlFor="provider-daily-limit" className="text-sm font-medium">{t('lockey_notifications_providers_form_daily_limit')}</label>
              <input
                id="provider-daily-limit"
                type="number"
                {...form.register('dailyLimit', { valueAsNumber: true })}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
              {form.formState.errors.dailyLimit?.message && (
                <p className="mt-1 text-sm text-destructive">{form.formState.errors.dailyLimit.message}</p>
              )}
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="isDefault"
                {...form.register('isDefault')}
                className="rounded border-input"
              />
              <label htmlFor="isDefault" className="text-sm font-medium">
                {t('lockey_notifications_providers_form_is_default')}
              </label>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button
                type="submit"
                disabled={createProvider.isPending || updateProvider.isPending}
              >
                {editingProvider
                  ? t('lockey_common_save', { ns: 'common' })
                  : t('lockey_notifications_providers_create')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Test Dialog */}
      <Dialog open={testDialogOpen} onOpenChange={setTestDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_notifications_providers_test_title')}</DialogTitle>
          </DialogHeader>
          <form onSubmit={testForm.handleSubmit(onTest)} className="space-y-4">
            <div>
              <label htmlFor="provider-test-recipient" className="text-sm font-medium">{t('lockey_notifications_providers_test_form_recipient')}</label>
              <input
                id="provider-test-recipient"
                type="text"
                {...testForm.register('testRecipient')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
              {testForm.formState.errors.testRecipient?.message && (
                <p className="mt-1 text-sm text-destructive">{testForm.formState.errors.testRecipient.message}</p>
              )}
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setTestDialogOpen(false)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button type="submit" disabled={testProvider.isPending}>
                {t('lockey_notifications_providers_test_action')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
