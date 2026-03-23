import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import type { ScheduleStatus } from '../types';
import { SCHEDULE_STATUS_KEY_MAP, SCHEDULE_STATUS_VARIANT_MAP } from '../constants';

interface ScheduleStatusBadgeProps {
  status: ScheduleStatus;
}

export function ScheduleStatusBadge({ status }: ScheduleStatusBadgeProps) {
  const { t } = useTranslation('notifications');

  return (
    <Badge variant={SCHEDULE_STATUS_VARIANT_MAP[status]}>
      {t(SCHEDULE_STATUS_KEY_MAP[status])}
    </Badge>
  );
}
