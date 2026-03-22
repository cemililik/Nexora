import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import type { ContactActivityDto } from '../types';

interface ActivityTimelineProps {
  activities: ContactActivityDto[];
}

export function ActivityTimeline({ activities }: ActivityTimelineProps) {
  const { t, i18n } = useTranslation('contacts');

  if (activities.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        {t('lockey_contacts_activities_empty')}
      </p>
    );
  }

  return (
    <div className="relative space-y-4">
      <div className="absolute start-3 top-0 bottom-0 w-px bg-border" />
      {activities.map((activity) => (
        <div key={activity.id} className="relative flex gap-4 ps-8">
          <div className="absolute start-1.5 top-1.5 h-3 w-3 rounded-full border-2 border-primary bg-background" />
          <div className="flex-1 space-y-1">
            <div className="flex items-center gap-2">
              <Badge variant="secondary" className="text-xs">
                {activity.moduleSource}
              </Badge>
              <span className="text-xs text-muted-foreground">
                {activity.activityType}
              </span>
            </div>
            <p className="text-sm">{activity.summary}</p>
            {activity.details && (
              <p className="text-xs text-muted-foreground">{activity.details}</p>
            )}
            <p className="text-xs text-muted-foreground">
              {new Date(activity.occurredAt).toLocaleString(i18n.language)}
            </p>
          </div>
        </div>
      ))}
    </div>
  );
}
