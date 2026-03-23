import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import type { NotificationStatus } from '../types';
import { STATUS_KEY_MAP, STATUS_VARIANT_MAP } from '../constants';

interface NotificationStatusBadgeProps {
  status: NotificationStatus;
}

export function NotificationStatusBadge({ status }: NotificationStatusBadgeProps) {
  const { t } = useTranslation('notifications');

  return (
    <Badge variant={STATUS_VARIANT_MAP[status]}>
      {t(STATUS_KEY_MAP[status])}
    </Badge>
  );
}
