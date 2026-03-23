import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/components/ui/select';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { useApiError } from '@/shared/hooks/useApiError';
import { useSendNotification } from '../hooks/useNotifications';
import { CHANNELS, CHANNEL_KEY_MAP } from '../constants';

function createSendSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    channel: z.enum([...CHANNELS], { message: t('lockey_notifications_validation_channel_required') }),
    contactId: z.string().min(1, t('lockey_notifications_validation_contact_id_required')),
    recipientAddress: z.string().min(1, t('lockey_notifications_validation_recipient_address_required')),
    templateCode: z.string().optional(),
    subject: z.string().optional(),
    body: z.string().optional(),
    variables: z.string().optional(),
    languageCode: z.string().optional(),
  });
}

type SendFormValues = z.infer<ReturnType<typeof createSendSchema>>;

export default function SendNotificationPage() {
  const { t } = useTranslation('notifications');
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const sendNotification = useSendNotification();
  const { handleApiError } = useApiError();

  const schema = useMemo(() => createSendSchema(t), [t]);
  const form = useForm<SendFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      channel: 'Email',
      contactId: '',
      recipientAddress: '',
      templateCode: '',
      subject: '',
      body: '',
      variables: '',
      languageCode: '',
    },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_notifications_module_name' },
      { label: 'lockey_notifications_send_title' },
    ]);
  }, [setBreadcrumbs]);

  const onSubmit = (values: SendFormValues) => {
    let variables: Record<string, string> | undefined;
    if (values.variables) {
      try {
        variables = JSON.parse(values.variables) as Record<string, string>;
      } catch {
        form.setError('variables', {
          message: t('lockey_validation_invalid_json', { ns: 'validation' }),
        });
        return;
      }
    }

    sendNotification.mutate(
      {
        channel: values.channel,
        contactId: values.contactId,
        recipientAddress: values.recipientAddress,
        templateCode: values.templateCode || undefined,
        subject: values.subject || undefined,
        body: values.body || undefined,
        variables,
        languageCode: values.languageCode || undefined,
      },
      {
        onSuccess: () => {
          navigate('/notifications/notifications');
        },
        onError: (err) => handleApiError(err),
      },
    );
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('lockey_notifications_send_title')}</h1>
        <p className="text-sm text-muted-foreground">
          {t('lockey_notifications_send_description')}
        </p>
      </div>

      <form onSubmit={form.handleSubmit(onSubmit)} className="max-w-2xl space-y-4">
        <div>
          <label htmlFor="send-channel" className="text-sm font-medium">{t('lockey_notifications_send_form_channel')}</label>
          <Controller
            control={form.control}
            name="channel"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="send-channel" className="mt-1">
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
            )}
          />
        </div>

        <div>
          <label htmlFor="send-contact-id" className="text-sm font-medium">{t('lockey_notifications_send_form_contact_id')}</label>
          <input
            id="send-contact-id"
            type="text"
            {...form.register('contactId')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
          {form.formState.errors.contactId?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.contactId.message}</p>
          )}
        </div>

        <div>
          <label htmlFor="send-recipient-address" className="text-sm font-medium">{t('lockey_notifications_send_form_recipient_address')}</label>
          <input
            id="send-recipient-address"
            type="text"
            {...form.register('recipientAddress')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
          {form.formState.errors.recipientAddress?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.recipientAddress.message}</p>
          )}
        </div>

        <div>
          <label htmlFor="send-template-code" className="text-sm font-medium">{t('lockey_notifications_send_form_template_code')}</label>
          <input
            id="send-template-code"
            type="text"
            {...form.register('templateCode')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
          <p className="mt-1 text-xs text-muted-foreground">
            {t('lockey_notifications_send_form_template_or_content')}
          </p>
        </div>

        <div>
          <label htmlFor="send-subject" className="text-sm font-medium">{t('lockey_notifications_send_form_subject')}</label>
          <input
            id="send-subject"
            type="text"
            {...form.register('subject')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
        </div>

        <div>
          <label htmlFor="send-body" className="text-sm font-medium">{t('lockey_notifications_send_form_body')}</label>
          <textarea
            id="send-body"
            {...form.register('body')}
            rows={5}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
        </div>

        <div>
          <label htmlFor="send-variables" className="text-sm font-medium">{t('lockey_notifications_send_form_variables')}</label>
          <textarea
            id="send-variables"
            {...form.register('variables')}
            rows={3}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm font-mono"
            placeholder={t('lockey_notifications_send_form_variables_placeholder')}
          />
        </div>

        <div>
          <label htmlFor="send-language-code" className="text-sm font-medium">{t('lockey_notifications_send_form_language_code')}</label>
          <input
            id="send-language-code"
            type="text"
            {...form.register('languageCode')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            placeholder={t('lockey_notifications_send_form_language_code_placeholder')}
          />
        </div>

        <div className="flex gap-2 pt-2">
          <Button type="button" variant="outline" onClick={() => navigate('/notifications/notifications')}>
            {t('lockey_common_cancel', { ns: 'common' })}
          </Button>
          <Button type="submit" disabled={sendNotification.isPending}>
            {t('lockey_notifications_send_action')}
          </Button>
        </div>
      </form>
    </div>
  );
}
