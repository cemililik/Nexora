import { useTranslation } from 'react-i18next';

import { Skeleton } from '@/shared/components/ui/skeleton';
import { cn } from '@/shared/lib/utils';

interface TabContentSkeletonProps {
  variant?: 'list' | 'form' | 'cards';
  className?: string;
}

/** Skeleton loader for tab content during data fetching. */
export function TabContentSkeleton({ variant = 'list', className }: TabContentSkeletonProps) {
  const { t } = useTranslation();

  return (
    <div role="status" aria-label={t('lockey_common_loading')} className={cn('p-4', className)}>
      {variant === 'list' && <ListSkeleton />}
      {variant === 'form' && <FormSkeleton />}
      {variant === 'cards' && <CardsSkeleton />}
      <span className="sr-only">{t('lockey_common_loading')}</span>
    </div>
  );
}

function ListSkeleton() {
  return (
    <div className="space-y-3">
      {Array.from({ length: 5 }, (_, i) => (
        <Skeleton key={i} className="h-12 w-full" />
      ))}
    </div>
  );
}

function FormSkeleton() {
  return (
    <div className="space-y-6">
      {Array.from({ length: 4 }, (_, i) => (
        <div key={i} className="space-y-2">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-10 w-full" />
        </div>
      ))}
    </div>
  );
}

function CardsSkeleton() {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
      {Array.from({ length: 4 }, (_, i) => (
        <Skeleton key={i} className="h-32 w-full rounded-lg" />
      ))}
    </div>
  );
}
