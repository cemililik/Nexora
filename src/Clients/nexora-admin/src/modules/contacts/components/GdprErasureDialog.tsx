import { useEffect, useMemo } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Textarea } from '@/shared/components/ui/textarea';
import { FormField } from '@/shared/components/data/FormField';

import { useGdprErasure } from '../hooks/useGdprErasure';

/** Props for {@link GdprErasureDialog}. */
export interface GdprErasureDialogProps {
  /** The contact's unique identifier. */
  contactId: string;
  /** The contact's resolved display name. Used for the confirm-by-name gate. */
  contactDisplayName: string;
  /** Whether the dialog is currently open. */
  open: boolean;
  /** Callback when the open state changes. */
  onOpenChange: (open: boolean) => void;
}

const REASON_MIN = 10;
const REASON_MAX = 500;

/**
 * Destructive GDPR Article 17 right-to-erasure dialog.
 *
 * Requires (a) a typed reason (10–500 chars) and (b) the operator to type the
 * contact's display name verbatim before the destructive submit is enabled.
 */
export function GdprErasureDialog({
  contactId,
  contactDisplayName,
  open,
  onOpenChange,
}: GdprErasureDialogProps) {
  const { t } = useTranslation('contacts');
  const mutation = useGdprErasure(contactId);

  const schema = useMemo(
    () =>
      z.object({
        reason: z
          .string()
          .trim()
          .min(REASON_MIN, { message: t('gdpr_erasure_reason_required') })
          .max(REASON_MAX, { message: t('gdpr_erasure_reason_required') }),
        confirmName: z.string(),
      }),
    [t],
  );

  type FormValues = z.infer<typeof schema>;

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isValid },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    mode: 'onChange',
    defaultValues: { reason: '', confirmName: '' },
  });

  const confirmName = useWatch({ control, name: 'confirmName' });
  const nameMatches = confirmName === contactDisplayName;

  // Reset the form whenever the dialog is re-opened so stale input never lingers.
  useEffect(() => {
    if (open) {
      reset({ reason: '', confirmName: '' });
    }
  }, [open, reset]);

  const onSubmit = handleSubmit((values) => {
    if (!nameMatches) return;
    mutation.mutate(
      { reason: values.reason.trim() },
      {
        onSuccess: () => {
          reset({ reason: '', confirmName: '' });
          onOpenChange(false);
        },
      },
    );
  });

  const submitDisabled = !isValid || !nameMatches || mutation.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('gdpr_erasure_dialog_title')}</DialogTitle>
          <DialogDescription>
            {t('gdpr_erasure_dialog_warning')}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} className="space-y-4">
          <FormField
            label={t('gdpr_erasure_reason_label')}
            htmlFor="gdpr-erasure-reason"
            required
            error={errors.reason?.message}
          >
            <Textarea
              id="gdpr-erasure-reason"
              rows={4}
              placeholder={t('gdpr_erasure_reason_placeholder')}
              maxLength={REASON_MAX}
              {...register('reason')}
            />
          </FormField>

          <FormField
            label={t('gdpr_erasure_confirm_label')}
            htmlFor="gdpr-erasure-confirm-name"
            required
            hint={contactDisplayName}
            error={
              confirmName.length > 0 && !nameMatches
                ? t('gdpr_erasure_name_mismatch')
                : undefined
            }
          >
            <Input
              id="gdpr-erasure-confirm-name"
              autoComplete="off"
              {...register('confirmName')}
            />
          </FormField>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={mutation.isPending}
            >
              {t('gdpr_erasure_cancel')}
            </Button>
            <Button
              type="submit"
              variant="destructive"
              disabled={submitDisabled}
            >
              {t('gdpr_erasure_confirm_action')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
