import { useTranslation } from 'react-i18next';

import { Badge } from '@/shared/components/ui/badge';
import { cn } from '@/shared/lib/utils';
import type { UserStatus, TenantStatus } from '../types';

const userStatusStyles: Record<UserStatus, string> = {
  Active: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
  Inactive: 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200',
  Locked: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
};

const userStatusKeys: Record<UserStatus, string> = {
  Active: 'lockey_identity_status_active',
  Inactive: 'lockey_identity_status_inactive',
  Locked: 'lockey_identity_status_locked',
};

interface UserStatusBadgeProps {
  status: UserStatus;
}

export function UserStatusBadge({ status }: UserStatusBadgeProps) {
  const { t } = useTranslation('identity');
  return (
    <Badge variant="outline" className={cn(userStatusStyles[status])}>
      {t(userStatusKeys[status])}
    </Badge>
  );
}

const tenantStatusStyles: Record<TenantStatus, string> = {
  Trial: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
  Active: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
  Suspended: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200',
  Terminated: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
};

const tenantStatusKeys: Record<TenantStatus, string> = {
  Trial: 'lockey_identity_status_trial',
  Active: 'lockey_identity_status_active',
  Suspended: 'lockey_identity_status_suspended',
  Terminated: 'lockey_identity_status_terminated',
};

interface TenantStatusBadgeProps {
  status: TenantStatus;
}

export function TenantStatusBadge({ status }: TenantStatusBadgeProps) {
  const { t } = useTranslation('identity');
  return (
    <Badge variant="outline" className={cn(tenantStatusStyles[status])}>
      {t(tenantStatusKeys[status])}
    </Badge>
  );
}
