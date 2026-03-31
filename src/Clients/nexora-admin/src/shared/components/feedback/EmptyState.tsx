import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';

interface EmptyStateAction {
  label: string;
  onClick: () => void;
}

interface EmptyStateProps {
  icon?: LucideIcon;
  title: string;
  description?: string;
  action?: EmptyStateAction | ReactNode;
  className?: string;
}

/** Empty state placeholder with icon, message, and optional call-to-action. */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div className={cn('flex flex-col items-center justify-center p-16 gap-4 text-center', className)}>
      {Icon && <Icon className="h-12 w-12 text-muted-foreground/40" />}
      <div>
        <p className="font-semibold text-foreground">{title}</p>
        {description && (
          <p className="text-sm text-muted-foreground mt-1">{description}</p>
        )}
      </div>
      {action && (
        isActionObject(action) ? (
          <Button onClick={action.onClick}>{action.label}</Button>
        ) : (
          action
        )
      )}
    </div>
  );
}

function isActionObject(action: EmptyStateAction | ReactNode): action is EmptyStateAction {
  return (
    typeof action === 'object' &&
    action !== null &&
    'label' in action &&
    'onClick' in action
  );
}
