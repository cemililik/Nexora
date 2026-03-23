import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import type { RecipientStatus } from '../types';
import { RECIPIENT_STATUS_KEY_MAP, RECIPIENT_STATUS_VARIANT_MAP } from '../constants';

interface RecipientStatusBadgeProps {
  status: RecipientStatus;
}

export function RecipientStatusBadge({ status }: RecipientStatusBadgeProps) {
  const { t } = useTranslation('notifications');

  return (
    <Badge variant={RECIPIENT_STATUS_VARIANT_MAP[status]}>
      {t(RECIPIENT_STATUS_KEY_MAP[status])}
    </Badge>
  );
}
