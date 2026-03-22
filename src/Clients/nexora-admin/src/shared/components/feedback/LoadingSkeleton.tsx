import { useTranslation } from 'react-i18next';

import { cn } from '@/shared/lib/utils';

interface LoadingSkeletonProps {
  className?: string;
  lines?: number;
}

/** Reusable loading skeleton with accessibility attributes. */
export function LoadingSkeleton({ className, lines = 3 }: LoadingSkeletonProps) {
  const { t } = useTranslation();

  return (
    <div role="status" aria-label={t('lockey_common_loading')} className={cn('space-y-3', className)}>
      {Array.from({ length: lines }, (_, i) => (
        <div
          key={i}
          className={cn(
            'h-4 animate-pulse rounded bg-muted',
            i === lines - 1 && 'w-2/3',
          )}
        />
      ))}
      <span className="sr-only">{t('lockey_common_loading')}</span>
    </div>
  );
}
