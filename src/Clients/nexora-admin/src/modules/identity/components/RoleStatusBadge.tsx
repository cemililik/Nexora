import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';

interface RoleStatusBadgeProps {
  isActive: boolean;
}

export function RoleStatusBadge({ isActive }: RoleStatusBadgeProps) {
  const { t } = useTranslation('identity');
  return (
    <Badge
      variant="outline"
      className={cn(
        isActive
          ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
          : 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200',
      )}
    >
      {isActive ? t('lockey_identity_status_active') : t('lockey_identity_status_inactive')}
    </Badge>
  );
}
