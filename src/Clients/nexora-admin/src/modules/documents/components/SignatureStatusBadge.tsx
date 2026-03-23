import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import type { SignatureRequestStatus, SignatureRecipientStatus } from '../types';

const REQUEST_STATUS_MAP: Record<SignatureRequestStatus, string> = {
  Draft: 'lockey_documents_signatures_status_draft',
  Sent: 'lockey_documents_signatures_status_sent',
  PartiallySigned: 'lockey_documents_signatures_status_partially_signed',
  Completed: 'lockey_documents_signatures_status_completed',
  Cancelled: 'lockey_documents_signatures_status_cancelled',
  Expired: 'lockey_documents_signatures_status_expired',
};

const REQUEST_VARIANT_MAP: Record<SignatureRequestStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Draft: 'outline',
  Sent: 'default',
  PartiallySigned: 'secondary',
  Completed: 'default',
  Cancelled: 'destructive',
  Expired: 'secondary',
};

const RECIPIENT_STATUS_MAP: Record<SignatureRecipientStatus, string> = {
  Pending: 'lockey_documents_signatures_recipient_status_pending',
  Viewed: 'lockey_documents_signatures_recipient_status_viewed',
  Signed: 'lockey_documents_signatures_recipient_status_signed',
  Declined: 'lockey_documents_signatures_recipient_status_declined',
  Expired: 'lockey_documents_signatures_recipient_status_expired',
};

const RECIPIENT_VARIANT_MAP: Record<SignatureRecipientStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'outline',
  Viewed: 'secondary',
  Signed: 'default',
  Declined: 'destructive',
  Expired: 'secondary',
};

interface SignatureRequestStatusBadgeProps {
  status: SignatureRequestStatus;
}

export function SignatureRequestStatusBadge({ status }: SignatureRequestStatusBadgeProps) {
  const { t } = useTranslation('documents');

  return (
    <Badge variant={REQUEST_VARIANT_MAP[status]}>
      {t(REQUEST_STATUS_MAP[status])}
    </Badge>
  );
}

interface SignatureRecipientStatusBadgeProps {
  status: SignatureRecipientStatus;
}

export function SignatureRecipientStatusBadge({ status }: SignatureRecipientStatusBadgeProps) {
  const { t } = useTranslation('documents');

  return (
    <Badge variant={RECIPIENT_VARIANT_MAP[status]}>
      {t(RECIPIENT_STATUS_MAP[status])}
    </Badge>
  );
}
