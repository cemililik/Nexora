import { useTranslation } from 'react-i18next';

import { cn } from '@/shared/lib/utils';

const statusColors: Record<string, string> = {
  Queued: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300',
  Running: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300',
  Completed: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300',
  Failed: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300',
};

interface ReportStatusBadgeProps {
  status: string;
}

export function ReportStatusBadge({ status }: ReportStatusBadgeProps) {
  const { t } = useTranslation('reporting');

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
        statusColors[status] ?? 'bg-gray-100 text-gray-800',
      )}
    >
      {t(`lockey_reporting_status_${status.toLowerCase()}`)}
    </span>
  );
}
