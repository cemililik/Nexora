import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import type { DocumentStatus } from '../types';

const STATUS_KEY_MAP: Record<DocumentStatus, string> = {
  Active: 'lockey_documents_status_active',
  Archived: 'lockey_documents_status_archived',
  Deleted: 'lockey_documents_status_deleted',
  PendingRender: 'lockey_documents_status_pending_render',
};

const STATUS_VARIANT_MAP: Record<DocumentStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Active: 'default',
  Archived: 'secondary',
  Deleted: 'destructive',
  PendingRender: 'outline',
};

interface DocumentStatusBadgeProps {
  status: DocumentStatus;
}

export function DocumentStatusBadge({ status }: DocumentStatusBadgeProps) {
  const { t } = useTranslation('documents');

  return (
    <Badge variant={STATUS_VARIANT_MAP[status]}>
      {t(STATUS_KEY_MAP[status])}
    </Badge>
  );
}
