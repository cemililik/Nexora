import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import type { NotificationChannel } from '../types';
import { CHANNEL_KEY_MAP } from '../constants';

interface ChannelBadgeProps {
  channel: NotificationChannel;
}

export function ChannelBadge({ channel }: ChannelBadgeProps) {
  const { t } = useTranslation('notifications');

  return (
    <Badge variant="outline">
      {t(CHANNEL_KEY_MAP[channel])}
    </Badge>
  );
}
