import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';

interface AuditStatusBadgeProps {
  isSuccess: boolean;
}

export function AuditStatusBadge({ isSuccess }: AuditStatusBadgeProps) {
  const { t } = useTranslation('audit');

  return (
    <Badge
      className={
        isSuccess
          ? 'border-transparent bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
          : 'border-transparent bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200'
      }
    >
      {isSuccess ? t('lockey_audit_status_success') : t('lockey_audit_status_failed')}
    </Badge>
  );
}
