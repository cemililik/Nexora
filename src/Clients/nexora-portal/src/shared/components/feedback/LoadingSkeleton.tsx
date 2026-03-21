import { cn } from '@/shared/lib/utils';

interface LoadingSkeletonProps {
  className?: string;
}

/** Reusable skeleton loader for page transitions. */
export function LoadingSkeleton({ className }: LoadingSkeletonProps) {
  return (
    <div className={cn('animate-pulse space-y-4', className)}>
      <div className="h-8 w-1/3 rounded-md bg-muted" />
      <div className="space-y-3">
        <div className="h-4 w-full rounded-md bg-muted" />
        <div className="h-4 w-5/6 rounded-md bg-muted" />
        <div className="h-4 w-4/6 rounded-md bg-muted" />
      </div>
      <div className="grid grid-cols-3 gap-4">
        <div className="h-24 rounded-md bg-muted" />
        <div className="h-24 rounded-md bg-muted" />
        <div className="h-24 rounded-md bg-muted" />
      </div>
    </div>
  );
}
