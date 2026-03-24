import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { Button } from '@/shared/components/ui/button';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useApiError } from '@/shared/hooks/useApiError';
import { useCreateSignatureRequest } from '../hooks/useSignatures';
import type { SignatureRecipientInput } from '../types';

function createSignatureSchema(t: (key: string, options?: Record<string, unknown>) => string) {
  return z.object({
    documentId: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
    title: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
    expiresAt: z.string().optional(),
    recipients: z.array(z.object({
      contactId: z.string().optional(),
      email: z.string().email(t('lockey_validation_email', { ns: 'validation' })),
      name: z.string().min(1, t('lockey_validation_required', { ns: 'validation' })),
      signingOrder: z.number(),
    })).min(1, t('lockey_documents_signatures_recipients_required', { ns: 'documents' })),
  });
}

type SignatureFormValues = z.infer<ReturnType<typeof createSignatureSchema>>;

export default function SignatureCreatePage() {
  const navigate = useNavigate();
  const { t } = useTranslation('documents');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canCreate = hasPermission('documents.signature.create');
  const { handleApiError } = useApiError();
  const createSignature = useCreateSignatureRequest();

  const [recipientName, setRecipientName] = useState('');
  const [recipientEmail, setRecipientEmail] = useState('');
  const [recipientContactId, setRecipientContactId] = useState('');

  const schema = useMemo(() => createSignatureSchema(t), [t]);
  const form = useForm<SignatureFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { documentId: '', title: '', expiresAt: '', recipients: [] },
  });

  const { fields, append, replace } = useFieldArray({
    control: form.control,
    name: 'recipients',
  });

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_signatures_title' },
      { label: 'lockey_documents_signatures_create' },
    ]);
  }, [setBreadcrumbs]);

  const addRecipient = () => {
    if (!recipientName || !recipientEmail || !recipientContactId) return;
    append({
      contactId: recipientContactId,
      email: recipientEmail,
      name: recipientName,
      signingOrder: fields.length + 1,
    });
    setRecipientName('');
    setRecipientEmail('');
    setRecipientContactId('');
  };

  const removeRecipient = (index: number) => {
    const currentRecipients = form.getValues('recipients');
    const updated = currentRecipients
      .filter((_, i) => i !== index)
      .map((r, i) => ({ ...r, signingOrder: i + 1 }));
    replace(updated);
  };

  const onSubmit = (values: SignatureFormValues) => {
    if (!canCreate) return;
    createSignature.mutate(
      {
        documentId: values.documentId,
        title: values.title,
        expiresAt: values.expiresAt || undefined,
        recipients: values.recipients as SignatureRecipientInput[],
      },
      { onSuccess: () => navigate('/documents/signatures'), onError: (err) => handleApiError(err) },
    );
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">{t('lockey_documents_signatures_create')}</h1>

      <form onSubmit={form.handleSubmit(onSubmit)} className="max-w-2xl space-y-4">
        <div>
          <label className="text-sm font-medium">{t('lockey_documents_signatures_form_document_id')}</label>
          <input
            type="text"
            {...form.register('documentId')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
          {form.formState.errors.documentId?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.documentId.message}</p>
          )}
        </div>

        <div>
          <label className="text-sm font-medium">{t('lockey_documents_signatures_form_title')}</label>
          <input
            type="text"
            {...form.register('title')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
          {form.formState.errors.title?.message && (
            <p className="mt-1 text-sm text-destructive">{form.formState.errors.title.message}</p>
          )}
        </div>

        <div>
          <label className="text-sm font-medium">{t('lockey_documents_signatures_form_expires_at')}</label>
          <input
            type="date"
            {...form.register('expiresAt')}
            className="mt-1 block w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          />
        </div>

        {/* Recipients */}
        <div className="space-y-3">
          <h3 className="text-sm font-semibold">{t('lockey_documents_signatures_col_recipients')}</h3>

          {fields.length > 0 && (
            <div className="rounded-lg border">
              <table className="w-full text-sm" aria-label={t('lockey_documents_signatures_col_recipients')}>
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-3 py-2 text-start">#</th>
                    <th className="px-3 py-2 text-start">{t('lockey_documents_signatures_form_recipient_name')}</th>
                    <th className="px-3 py-2 text-start">{t('lockey_documents_signatures_form_recipient_email')}</th>
                    <th className="px-3 py-2 text-start">{t('lockey_documents_col_actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {fields.map((r, idx) => (
                    <tr key={r.id} className="border-b last:border-0">
                      <td className="px-3 py-2">{r.signingOrder}</td>
                      <td className="px-3 py-2">{r.name}</td>
                      <td className="px-3 py-2">{r.email}</td>
                      <td className="px-3 py-2">
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => removeRecipient(idx)}
                        >
                          {t('lockey_documents_signatures_remove_recipient')}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="grid grid-cols-3 gap-2">
            <input
              type="text"
              value={recipientName}
              onChange={(e) => setRecipientName(e.target.value)}
              placeholder={t('lockey_documents_signatures_form_recipient_name')}
              className="rounded-md border border-input bg-background px-3 py-2 text-sm"
            />
            <input
              type="email"
              value={recipientEmail}
              onChange={(e) => setRecipientEmail(e.target.value)}
              placeholder={t('lockey_documents_signatures_form_recipient_email')}
              className="rounded-md border border-input bg-background px-3 py-2 text-sm"
            />
            <input
              type="text"
              value={recipientContactId}
              onChange={(e) => setRecipientContactId(e.target.value)}
              placeholder={t('lockey_documents_signatures_form_recipient_contact_id')}
              className="rounded-md border border-input bg-background px-3 py-2 text-sm"
            />
          </div>
          <Button type="button" variant="outline" size="sm" onClick={addRecipient}>
            {t('lockey_documents_signatures_add_recipient')}
          </Button>
        </div>

        <div className="flex gap-2">
          <Button type="button" variant="outline" onClick={() => navigate('/documents/signatures')}>
            {t('lockey_common_cancel', { ns: 'common' })}
          </Button>
          {canCreate && (
            <Button
              type="submit"
              disabled={createSignature.isPending || fields.length === 0}
            >
              {t('lockey_documents_signatures_create')}
            </Button>
          )}
        </div>
      </form>
    </div>
  );
}
