import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Loader2 } from 'lucide-react';

import { Input } from '@/shared/components/ui/input';
import { cn } from '@/shared/lib/utils';

interface SearchableDropdownProps<T> {
  value: T | null;
  onSelect: (item: T) => void;
  items: T[];
  isLoading?: boolean;
  searchValue: string;
  onSearchChange: (value: string) => void;
  renderItem: (item: T) => ReactNode;
  renderSelected?: (item: T) => string;
  keyExtractor: (item: T) => string;
  placeholder?: string;
  emptyMessage?: string;
  label: string;
  className?: string;
}

/** Searchable dropdown with loading and empty states, keyboard navigation. */
export function SearchableDropdown<T>({
  value,
  onSelect,
  items,
  isLoading = false,
  searchValue,
  onSearchChange,
  renderItem,
  renderSelected,
  keyExtractor,
  placeholder,
  emptyMessage,
  label,
  className,
}: SearchableDropdownProps<T>) {
  const { t } = useTranslation('common');
  const inputId = useId();
  const listboxId = useId();
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const resolvedPlaceholder = placeholder ?? t('lockey_common_search');
  const resolvedEmptyMessage = emptyMessage ?? t('lockey_common_no_results');

  // Display selected value or search text
  const displayValue = value && renderSelected && !open
    ? renderSelected(value)
    : searchValue;

  const handleSelect = useCallback(
    (item: T) => {
      onSelect(item);
      setOpen(false);
      setActiveIndex(-1);
    },
    [onSelect],
  );

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (!open) {
        if (e.key === 'ArrowDown' || e.key === 'Enter') {
          setOpen(true);
          e.preventDefault();
        }
        return;
      }

      switch (e.key) {
        case 'ArrowDown':
          e.preventDefault();
          setActiveIndex((prev) => (prev < items.length - 1 ? prev + 1 : prev));
          break;
        case 'ArrowUp':
          e.preventDefault();
          setActiveIndex((prev) => (prev > 0 ? prev - 1 : prev));
          break;
        case 'Enter': {
          e.preventDefault();
          const selectedItem = activeIndex >= 0 ? items[activeIndex] : undefined;
          if (selectedItem !== undefined) {
            handleSelect(selectedItem);
          }
          break;
        }
        case 'Escape':
          e.preventDefault();
          setOpen(false);
          setActiveIndex(-1);
          break;
      }
    },
    [open, items, activeIndex, handleSelect],
  );

  // Scroll active option into view
  useEffect(() => {
    if (activeIndex < 0 || !listRef.current) return;
    const options = listRef.current.querySelectorAll('[role="option"]');
    options[activeIndex]?.scrollIntoView({ block: 'nearest' });
  }, [activeIndex]);

  // Close dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
        setActiveIndex(-1);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  // Reset active index when items change
  useEffect(() => {
    setActiveIndex(-1);
  }, [items]);

  const showDropdown = open && (isLoading || items.length > 0 || searchValue.length > 0);

  return (
    <div ref={containerRef} className={cn('relative', className)}>
      <label htmlFor={inputId} className="text-sm font-medium">
        {label}
      </label>
      <Input
        id={inputId}
        type="text"
        value={displayValue}
        onChange={(e) => {
          onSearchChange(e.target.value);
          if (!open) setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={handleKeyDown}
        placeholder={resolvedPlaceholder}
        className="mt-1"
        autoComplete="off"
        role="combobox"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-activedescendant={activeIndex >= 0 ? `${listboxId}-${activeIndex}` : undefined}
        aria-label={label}
      />
      {showDropdown && (
        <div
          ref={listRef}
          id={listboxId}
          role="listbox"
          aria-label={label}
          className="absolute z-10 mt-1 max-h-48 w-full overflow-y-auto rounded-md border bg-popover shadow-md"
        >
          {isLoading ? (
            <div className="flex items-center justify-center gap-2 px-3 py-4 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t('lockey_common_searching')}
            </div>
          ) : items.length === 0 ? (
            <div className="px-3 py-4 text-center text-sm text-muted-foreground">
              {resolvedEmptyMessage}
            </div>
          ) : (
            items.map((item, index) => (
              <button
                key={keyExtractor(item)}
                id={`${listboxId}-${index}`}
                type="button"
                role="option"
                aria-selected={activeIndex === index}
                className={cn(
                  'flex w-full items-center gap-2 px-3 py-2 text-start text-sm hover:bg-muted',
                  activeIndex === index && 'bg-muted',
                )}
                onClick={() => handleSelect(item)}
                onMouseEnter={() => setActiveIndex(index)}
              >
                {renderItem(item)}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}
