import { useEffect, useState } from 'react';
import { useParams } from 'react-router';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { ConfirmDialog } from '@/shared/components/feedback/ConfirmDialog';
import { useApiError } from '@/shared/hooks/useApiError';
import { LoadingSkeleton } from '@/shared/components/feedback/LoadingSkeleton';
import { useUiStore } from '@/shared/lib/stores/uiStore';
import { usePermissions } from '@/shared/hooks/usePermissions';
import { useSignature, useSendSignatureRequest, useCancelSignatureRequest } from '../hooks/useSignatures';
import { SignatureRequestStatusBadge, SignatureRecipientStatusBadge } from '../components/SignatureStatusBadge';

export default function SignatureDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation('documents');
  const setBreadcrumbs = useUiStore((s) => s.setBreadcrumbs);
  const { hasPermission } = usePermissions();
  const canCreate = hasPermission('documents.signature.create');
  const { handleApiError } = useApiError();

  const { data: request, isPending } = useSignature(id ?? '');
  const sendRequest = useSendSignatureRequest();
  const cancelRequest = useCancelSignatureRequest();

  const [sendConfirm, setSendConfirm] = useState(false);
  const [cancelConfirm, setCancelConfirm] = useState(false);

  useEffect(() => {
    setBreadcrumbs([
      { label: 'lockey_documents_module_name' },
      { label: 'lockey_documents_signatures_title' },
      { label: request?.title ?? '...' },
    ]);
  }, [setBreadcrumbs, request?.title]);

  if (isPending) return <LoadingSkeleton />;
  if (!request) return null;

  const canSend = request.status === 'Draft' && canCreate;
  const canCancel =
    (request.status === 'Draft' || request.status === 'Sent' || request.status === 'PartiallySigned') &&
    canCreate;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{request.title}</h1>
          <div className="mt-1 flex items-center gap-2">
            <SignatureRequestStatusBadge status={request.status} />
            <span className="text-sm text-muted-foreground">
              {new Date(request.createdAt).toLocaleDateString(i18n.language)}
            </span>
          </div>
        </div>
        <div className="flex gap-2">
          {canSend && (
            <Button type="button" onClick={() => setSendConfirm(true)}>
              {t('lockey_documents_signatures_send')}
            </Button>
          )}
          {canCancel && (
            <Button type="button" variant="outline" onClick={() => setCancelConfirm(true)}>
              {t('lockey_documents_signatures_cancel')}
            </Button>
          )}
        </div>
      </div>

      {/* Request info */}
      <div className="grid grid-cols-2 gap-4 rounded-lg border p-4">
        <div>
          <p className="text-sm text-muted-foreground">{t('lockey_documents_signatures_col_document')}</p>
          <p className="text-sm font-medium">{request.documentId}</p>
        </div>
        {request.expiresAt && (
          <div>
            <p className="text-sm text-muted-foreground">{t('lockey_documents_signatures_col_expires_at')}</p>
            <p className="text-sm font-medium">{new Date(request.expiresAt).toLocaleDateString(i18n.language)}</p>
          </div>
        )}
        {request.completedAt && (
          <div>
            <p className="text-sm text-muted-foreground">{t('lockey_documents_col_updated_at')}</p>
            <p className="text-sm font-medium">{new Date(request.completedAt).toLocaleDateString(i18n.language)}</p>
          </div>
        )}
      </div>

      {/* Recipients table */}
      <div>
        <h2 className="mb-3 text-lg font-semibold">{t('lockey_documents_signatures_col_recipients')}</h2>
        {request.recipients.length > 0 ? (
          <div className="rounded-lg border">
            <table className="w-full text-sm" aria-label={t('lockey_documents_signatures_col_recipients')}>
              <thead>
                <tr className="border-b bg-muted/50">
                  <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_form_signing_order')}</th>
                  <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_form_recipient_name')}</th>
                  <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_form_recipient_email')}</th>
                  <th className="px-4 py-2 text-start">{t('lockey_documents_signatures_col_status')}</th>
                  <th className="px-4 py-2 text-start">{t('lockey_documents_col_updated_at')}</th>
                </tr>
              </thead>
              <tbody>
                {request.recipients.map((r) => (
                  <tr key={r.id} className="border-b last:border-0">
                    <td className="px-4 py-2">{r.signingOrder}</td>
                    <td className="px-4 py-2">{r.name}</td>
                    <td className="px-4 py-2">{r.email}</td>
                    <td className="px-4 py-2">
                      <SignatureRecipientStatusBadge status={r.status} />
                    </td>
                    <td className="px-4 py-2">
                      {r.signedAt
                        ? new Date(r.signedAt).toLocaleDateString(i18n.language)
                        : t('lockey_documents_signatures_not_signed')}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">{t('lockey_documents_signatures_empty')}</p>
        )}
      </div>

      {/* Send Confirm */}
      <ConfirmDialog
        open={sendConfirm}
        onOpenChange={setSendConfirm}
        title={t('lockey_documents_signatures_confirm_send_title')}
        description={t('lockey_documents_signatures_confirm_send')}
        onConfirm={() => {
          if (!id) return;
          sendRequest.mutate(id, {
            onSuccess: () => setSendConfirm(false),
            onError: (err) => handleApiError(err),
          });
        }}
        isPending={sendRequest.isPending}
      />

      {/* Cancel Confirm */}
      <ConfirmDialog
        open={cancelConfirm}
        onOpenChange={setCancelConfirm}
        title={t('lockey_documents_signatures_confirm_cancel_title')}
        description={t('lockey_documents_signatures_confirm_cancel')}
        variant="destructive"
        onConfirm={() => {
          if (!id) return;
          cancelRequest.mutate(id, {
            onSuccess: () => setCancelConfirm(false),
            onError: (err) => handleApiError(err),
          });
        }}
        isPending={cancelRequest.isPending}
      />
    </div>
  );
}
