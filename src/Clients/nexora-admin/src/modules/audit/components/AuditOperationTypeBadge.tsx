import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import type { AuditOperationType } from '../types';

interface AuditOperationTypeBadgeProps {
  operationType: string;
}

const TYPE_COLOR_MAP: Record<AuditOperationType, string> = {
  Create: 'border-transparent bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
  Update: 'border-transparent bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
  Delete: 'border-transparent bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
  Action: 'border-transparent bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200',
  Read: 'border-transparent bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200',
};

const TYPE_KEY_MAP: Record<AuditOperationType, string> = {
  Create: 'lockey_audit_type_create',
  Update: 'lockey_audit_type_update',
  Delete: 'lockey_audit_type_delete',
  Action: 'lockey_audit_type_action',
  Read: 'lockey_audit_type_read',
};

export function AuditOperationTypeBadge({ operationType }: AuditOperationTypeBadgeProps) {
  const { t } = useTranslation('audit');
  const type = operationType as AuditOperationType;
  const colorClass = TYPE_COLOR_MAP[type] ?? 'border-transparent bg-secondary text-secondary-foreground';
  const labelKey = TYPE_KEY_MAP[type];

  return (
    <Badge className={colorClass}>
      {labelKey ? t(labelKey) : operationType}
    </Badge>
  );
}
