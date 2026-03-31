import * as React from 'react';

import { Textarea } from '@/shared/components/ui/textarea';
import { cn } from '@/shared/lib/utils';

interface TextareaWithCounterProps extends React.ComponentProps<'textarea'> {
  maxLength: number;
}

/** Textarea with a live character counter below it. */
export const TextareaWithCounter = React.forwardRef<
  HTMLTextAreaElement,
  TextareaWithCounterProps
>(({ maxLength, className, value, defaultValue, onChange, ...props }, ref) => {
  const [length, setLength] = React.useState(() => {
    const initial = (value ?? defaultValue ?? '') as string;
    return initial.length;
  });

  const handleChange = React.useCallback(
    (e: React.ChangeEvent<HTMLTextAreaElement>) => {
      setLength(e.target.value.length);
      onChange?.(e);
    },
    [onChange],
  );

  // Sync length when controlled value changes
  React.useEffect(() => {
    if (value !== undefined) {
      setLength((value as string).length);
    }
  }, [value]);

  const isOver = length > maxLength;

  return (
    <div>
      <Textarea
        ref={ref}
        className={className}
        value={value}
        defaultValue={defaultValue}
        onChange={handleChange}
        maxLength={maxLength}
        {...props}
      />
      <p className={cn('mt-1 text-end text-xs', isOver ? 'text-destructive' : 'text-muted-foreground')}>
        {length}/{maxLength}
      </p>
    </div>
  );
});

TextareaWithCounter.displayName = 'TextareaWithCounter';
