import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, useNavigate } from 'react-router';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
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
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import {
  useNotificationTemplate,
  useCreateNotificationTemplate,
  useUpdateNotificationTemplate,
  useDeleteNotificationTemplate,
  useAddTemplateTranslation,
} from '../hooks/useNotificationTemplates';
import { CHANNELS, CHANNEL_KEY_MAP, FORMATS, FORMAT_KEY_MAP } from '../constants';

function createTemplateSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    code: z.string().min(1, t('lockey_notifications_validation_code_required')),
    module: z.string().min(1, t('lockey_notifications_validation_module_required')),
    channel: z.enum([...CHANNELS], { message: t('lockey_notifications_validation_channel_required') }),
    subject: z.string().min(1, t('lockey_notifications_validation_subject_required')),
    body: z.string().min(1, t('lockey_notifications_validation_body_required')),
    format: z.enum([...FORMATS], { message: t('lockey_notifications_validation_format_required') }),
  });
}

function createTranslationSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    languageCode: z.string().min(1, t('lockey_notifications_validation_language_code_required')),
    subject: z.string().min(1, t('lockey_notifications_validation_subject_required')),
    body: z.string().min(1, t('lockey_notifications_validation_body_required')),
  });
}

type TemplateFormValues = z.infer<ReturnType<typeof createTemplateSchema>>;
type TranslationFormValues = z.infer<ReturnType<typeof createTranslationSchema>>;

export default function TemplateDetailPage() {
  const { t } = useTranslation('notifications');
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canManage = hasPermission('notifications.template.manage');

  const isCreate = id === 'create';
  const { data: template, isPending } = useNotificationTemplate(id ?? '');

  const createTemplate = useCreateNotificationTemplate();
  const updateTemplate = useUpdateNotificationTemplate(id ?? '');
  const deleteTemplate = useDeleteNotificationTemplate();
  const addTranslation = useAddTemplateTranslation(id ?? '');

  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const [translationDialogOpen, setTranslationDialogOpen] = useState(false);

  const templateSchema = useMemo(() => createTemplateSchema(t), [t]);
  const translationSchema = useMemo(() => createTranslationSchema(t), [t]);

  const form = useForm<TemplateFormValues>({
    resolver: zodResolver(templateSchema),
    defaultValues: {
      code: '',
      module: '',
      channel: 'Email',
      subject: '',
      body: '',
      format: 'Html',
    },
  });

  const translationForm = useForm<TranslationFormValues>({
    resolver: zodResolver(translationSchema),
    defaultValues: { languageCode: '', subject: '', body: '' },
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_notifications_module_name' },
      { label: 'lockey_notifications_templates_title' },
      { label: isCreate ? 'lockey_notifications_templates_create' : 'lockey_notifications_templates_detail_title' },
    ]);
  }, [setBreadcrumbs, isCreate]);

  useEffect(() => {
    if (template && !isCreate) {
      form.reset({
        code: template.code,
        module: template.module,
        channel: template.channel,
        subject: template.subject,
        body: template.body,
        format: template.format,
      });
    }
  }, [template, isCreate, form]);

  if (!isCreate && isPending) return <LoadingSkeleton />;

  const onSubmit = (values: TemplateFormValues) => {
    if (isCreate) {
      createTemplate.mutate(
        {
          code: values.code,
          module: values.module,
          channel: values.channel,
          subject: values.subject,
          body: values.body,
          format: values.format,
        },
        { onSuccess: () => navigate('/notifications/templates') },
      );
    } else {
      updateTemplate.mutate(
        {
          subject: values.subject,
          body: values.body,
          format: values.format,
        },
        { onSuccess: () => navigate('/notifications/templates') },
      );
    }
  };

  const onAddTranslation = (values: TranslationFormValues) => {
    addTranslation.mutate(values, {
      onSuccess: () => {
        translationForm.reset();
        setTranslationDialogOpen(false);
      },
    });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">
          {isCreate ? t('lockey_notifications_templates_create') : t('lockey_notifications_templates_detail_title')}
        </h1>
        {!isCreate && canManage && (
          <Button
            type="button"
            variant="destructive"
            onClick={() => setDeleteConfirmOpen(true)}
          >
            {t('lockey_common_delete', { ns: 'common' })}
          </Button>
        )}
      </div>

      <form onSubmit={form.handleSubmit(onSubmit)} className="max-w-2xl space-y-4">
        <div>
          <label htmlFor="template-code" className="text-sm font-medium">{t('lockey_notifications_templates_form_code')}</label>
          <input
            id="template-code"
            type="text"
            {...form.register('code')}
            disabled={!isCreate}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm disabled:opacity-50"
          />
          {form.formState.errors.code?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.code.message}</p>
          )}
        </div>

        <div>
          <label htmlFor="template-module" className="text-sm font-medium">{t('lockey_notifications_templates_form_module')}</label>
          <input
            id="template-module"
            type="text"
            {...form.register('module')}
            disabled={!isCreate}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm disabled:opacity-50"
          />
          {form.formState.errors.module?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.module.message}</p>
          )}
        </div>

        <div>
          <label htmlFor="template-channel" className="text-sm font-medium">{t('lockey_notifications_templates_form_channel')}</label>
          <Controller
            control={form.control}
            name="channel"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange} disabled={!isCreate}>
                <SelectTrigger id="template-channel" className="mt-1">
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
          <label htmlFor="template-subject" className="text-sm font-medium">{t('lockey_notifications_templates_form_subject')}</label>
          <input
            id="template-subject"
            type="text"
            {...form.register('subject')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
          {form.formState.errors.subject?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.subject.message}</p>
          )}
        </div>

        <div>
          <label htmlFor="template-body" className="text-sm font-medium">{t('lockey_notifications_templates_form_body')}</label>
          <textarea
            id="template-body"
            {...form.register('body')}
            rows={10}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm font-mono"
          />
          {form.formState.errors.body?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.body.message}</p>
          )}
        </div>

        <div>
          <label htmlFor="template-format" className="text-sm font-medium">{t('lockey_notifications_templates_form_format')}</label>
          <Controller
            control={form.control}
            name="format"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="template-format" className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {FORMATS.map((f) => (
                    <SelectItem key={f} value={f}>
                      {t(FORMAT_KEY_MAP[f])}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>

        <div className="flex gap-2 pt-2">
          <Button type="button" variant="outline" onClick={() => navigate('/notifications/templates')}>
            {t('lockey_common_cancel', { ns: 'common' })}
          </Button>
          <Button
            type="submit"
            disabled={createTemplate.isPending || updateTemplate.isPending}
          >
            {isCreate
              ? t('lockey_notifications_templates_create')
              : t('lockey_common_save', { ns: 'common' })}
          </Button>
        </div>
      </form>

      {/* Translations section (edit mode only) */}
      {!isCreate && template && (
        <div className="max-w-2xl space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-medium">{t('lockey_notifications_translations_title')}</h2>
            {canManage && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                  translationForm.reset();
                  setTranslationDialogOpen(true);
                }}
              >
                {t('lockey_notifications_translations_add')}
              </Button>
            )}
          </div>

          {template.translations.length > 0 ? (
            <div className="rounded-lg border">
              <table className="w-full text-sm" aria-label={t('lockey_notifications_aria_translations_table')}>
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-4 py-2 text-start">{t('lockey_notifications_translations_col_language')}</th>
                    <th className="px-4 py-2 text-start">{t('lockey_notifications_translations_col_subject')}</th>
                  </tr>
                </thead>
                <tbody>
                  {template.translations.map((tr) => (
                    <tr key={tr.id} className="border-b last:border-0">
                      <td className="px-4 py-2 font-medium">{tr.languageCode}</td>
                      <td className="px-4 py-2">{tr.subject}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t('lockey_notifications_translations_empty')}</p>
          )}
        </div>
      )}

      {/* Add Translation Dialog */}
      <Dialog open={translationDialogOpen} onOpenChange={setTranslationDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('lockey_notifications_translations_add')}</DialogTitle>
            <DialogDescription className="sr-only">{t('lockey_notifications_translations_add')}</DialogDescription>
          </DialogHeader>
          <form onSubmit={translationForm.handleSubmit(onAddTranslation)} className="space-y-4">
            <div>
              <label htmlFor="translation-language-code" className="text-sm font-medium">{t('lockey_notifications_translations_form_language_code')}</label>
              <input
                id="translation-language-code"
                type="text"
                {...translationForm.register('languageCode')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                placeholder={t('lockey_notifications_translations_form_language_code_placeholder')}
              />
              {translationForm.formState.errors.languageCode?.message && (
                <p className="mt-1 text-sm text-destructive">{translationForm.formState.errors.languageCode.message}</p>
              )}
            </div>
            <div>
              <label htmlFor="translation-subject" className="text-sm font-medium">{t('lockey_notifications_translations_form_subject')}</label>
              <input
                id="translation-subject"
                type="text"
                {...translationForm.register('subject')}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
              {translationForm.formState.errors.subject?.message && (
                <p className="mt-1 text-sm text-destructive">{translationForm.formState.errors.subject.message}</p>
              )}
            </div>
            <div>
              <label htmlFor="translation-body" className="text-sm font-medium">{t('lockey_notifications_translations_form_body')}</label>
              <textarea
                id="translation-body"
                {...translationForm.register('body')}
                rows={5}
                className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              />
              {translationForm.formState.errors.body?.message && (
                <p className="mt-1 text-sm text-destructive">{translationForm.formState.errors.body.message}</p>
              )}
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setTranslationDialogOpen(false)}>
                {t('lockey_common_cancel', { ns: 'common' })}
              </Button>
              <Button type="submit" disabled={addTranslation.isPending}>
                {t('lockey_notifications_translations_add')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Delete Confirm */}
      <ConfirmDialog
        open={deleteConfirmOpen}
        onOpenChange={(open) => setDeleteConfirmOpen(open)}
        title={t('lockey_notifications_templates_confirm_delete_title')}
        description={t('lockey_notifications_templates_confirm_delete')}
        variant="destructive"
        onConfirm={() => {
          if (id) {
            deleteTemplate.mutate(id, {
              onSuccess: () => navigate('/notifications/templates'),
            });
          }
        }}
        isPending={deleteTemplate.isPending}
      />
    </div>
  );
}
