import React, { useId, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ArrowUp, ArrowDown, ArrowUpDown, SearchX } from 'lucide-react';

import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { EmptyState } from '@/shared/components/feedback/EmptyState';
import { cn } from '@/shared/lib/utils';

export type SortDirection = 'asc' | 'desc';

export interface ColumnDef<T> {
  key: string;
  header: ReactNode;
  render: (row: T) => ReactNode;
  className?: string;
  sortable?: boolean;
  sortKey?: string;
}

interface DataTableProps<T> {
  columns: ColumnDef<T>[];
  data: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
  pageSizeOptions?: number[];
  isLoading?: boolean;
  emptyMessage?: string;
  emptyState?: ReactNode;
  'aria-label'?: string;
  sortBy?: string;
  sortDirection?: SortDirection;
  onSortChange?: (key: string, direction: SortDirection) => void;
  selectable?: boolean;
  selectedKeys?: Set<string | number>;
  onSelectionChange?: (keys: Set<string | number>) => void;
  keyExtractor?: (row: T, index: number) => string | number;
  onRowClick?: (row: T) => void;
}

const INTERACTIVE_SELECTOR =
  'button, a, input, select, textarea, [role="button"], [role="link"], [role="checkbox"], [role="radio"], [role="switch"], [role="textbox"], [role="combobox"], [role="menuitem"], [role="tab"], [role="slider"], [role="spinbutton"], [contenteditable="true"]';

/** Returns true if the event target is (or is inside) an interactive child element. */
function isInteractiveChild(target: HTMLElement, currentTarget: EventTarget): boolean {
  const el = target.closest(INTERACTIVE_SELECTOR);
  return !!el && el !== currentTarget;
}

/** Generic data table with pagination and loading state. */
export function DataTable<T>({
  columns,
  data,
  totalCount,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions,
  isLoading = false,
  emptyMessage,
  emptyState,
  'aria-label': ariaLabel,
  sortBy,
  sortDirection,
  onSortChange,
  selectable = false,
  selectedKeys,
  onSelectionChange,
  keyExtractor = (_row, index) => index,
  onRowClick,
}: DataTableProps<T>) {
  const { t } = useTranslation();
  const pageSizeSelectId = useId();
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  if (isLoading) {
    return (
      <div role="status" aria-label={t('lockey_common_loading')} className="space-y-2">
        {Array.from({ length: 5 }, (_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {data.length === 0 ? (
        emptyState ?? (
          <EmptyState
            icon={SearchX}
            title={emptyMessage ?? t('lockey_common_no_results')}
          />
        )
      ) : (
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full text-sm" aria-label={ariaLabel}>
            <thead className="border-b bg-muted/50">
              <tr>
                {selectable && (
                  <th className="w-10 px-3 py-3">
                    <input
                      type="checkbox"
                      className="rounded border-input"
                      checked={data.length > 0 && data.every((row, i) => selectedKeys?.has(keyExtractor(row, i)))}
                      onChange={(e) => {
                        if (!onSelectionChange) return;
                        if (e.target.checked) {
                          const allKeys = new Set(data.map((row, i) => keyExtractor(row, i)));
                          onSelectionChange(allKeys);
                        } else {
                          onSelectionChange(new Set());
                        }
                      }}
                      aria-label={t('lockey_common_select_all')}
                    />
                  </th>
                )}
                {columns.map((col) => {
                  const colSortKey = col.sortKey ?? col.key;
                  const isActive = col.sortable && sortBy === colSortKey;
                  const handleSort = () => {
                    if (!col.sortable || !onSortChange) return;
                    const next: SortDirection = isActive && sortDirection === 'asc' ? 'desc' : 'asc';
                    onSortChange(colSortKey, next);
                  };

                  return (
                    <th
                      key={col.key}
                      className="px-4 py-3 text-start font-medium text-muted-foreground"
                      aria-sort={isActive ? (sortDirection === 'asc' ? 'ascending' : 'descending') : undefined}
                    >
                      {col.sortable ? (
                        <button
                          type="button"
                          className="inline-flex items-center gap-1 hover:text-foreground transition-colors"
                          onClick={handleSort}
                          aria-label={`${typeof col.header === 'string' ? col.header : ''} — ${t(isActive && sortDirection === 'asc' ? 'lockey_common_sort_descending' : 'lockey_common_sort_ascending')}`}
                        >
                          {col.header}
                          {isActive ? (
                            sortDirection === 'asc' ? <ArrowUp className="h-3.5 w-3.5" /> : <ArrowDown className="h-3.5 w-3.5" />
                          ) : (
                            <ArrowUpDown className="h-3.5 w-3.5 opacity-40" />
                          )}
                        </button>
                      ) : (
                        col.header
                      )}
                    </th>
                  );
                })}
              </tr>
            </thead>
            <tbody>
              {data.map((row, index) => {
                const rowKey = keyExtractor(row, index);
                return (
                <tr
                  key={rowKey}
                  className={cn('border-b last:border-0', onRowClick && 'cursor-pointer hover:bg-muted/50 dark:hover:bg-muted/60 transition-colors')}
                  tabIndex={onRowClick ? 0 : undefined}
                  onClick={onRowClick ? (e: React.MouseEvent<HTMLTableRowElement>) => {
                    if (isInteractiveChild(e.target as HTMLElement, e.currentTarget)) return;
                    onRowClick(row);
                  } : undefined}
                  onKeyDown={onRowClick ? (e: React.KeyboardEvent<HTMLTableRowElement>) => {
                    if (e.key !== 'Enter' && e.key !== ' ') return;
                    if (isInteractiveChild(e.target as HTMLElement, e.currentTarget)) return;
                    if (e.key === ' ') e.preventDefault();
                    onRowClick(row);
                  } : undefined}
                >
                  {selectable && (
                    <td className="w-10 px-3 py-3">
                      <input
                        type="checkbox"
                        className="rounded border-input"
                        checked={selectedKeys?.has(rowKey) ?? false}
                        onChange={(e) => {
                          if (!onSelectionChange || !selectedKeys) return;
                          const next = new Set(selectedKeys);
                          if (e.target.checked) {
                            next.add(rowKey);
                          } else {
                            next.delete(rowKey);
                          }
                          onSelectionChange(next);
                        }}
                        onClick={(e) => e.stopPropagation()}
                      />
                    </td>
                  )}
                  {columns.map((col) => (
                    <td key={col.key} className={col.className ?? 'px-4 py-3'}>
                      {col.render(row)}
                    </td>
                  ))}
                </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <span className="text-sm text-muted-foreground">
              {t('lockey_common_page_of', {
                page: String(page),
                totalPages: String(totalPages),
              })}
            </span>
            {onPageSizeChange && (
              <div className="flex items-center gap-2">
                <label htmlFor={pageSizeSelectId} className="text-sm text-muted-foreground">
                  {t('lockey_common_items_per_page')}
                </label>
                <select
                  id={pageSizeSelectId}
                  value={pageSize}
                  onChange={(e) => onPageSizeChange(Number(e.target.value))}
                  className="rounded-md border border-input bg-background px-2 py-1 text-sm"
                >
                  {(() => {
                  const options = pageSizeOptions ?? [10, 20, 50];
                  return options.includes(pageSize) ? options : [...options, pageSize].sort((a, b) => a - b);
                })().map((size) => (
                    <option key={size} value={size}>
                      {size}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => onPageChange(page - 1)}
              aria-label={t('lockey_common_previous_page')}
            >
              {t('lockey_common_previous')}
            </Button>
            {totalPages > 2 && (
              <PageJumpInput page={page} totalPages={totalPages} onPageChange={onPageChange} ariaLabel={t('lockey_common_go_to_page')} />
            )}
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={page >= totalPages}
              onClick={() => onPageChange(page + 1)}
              aria-label={t('lockey_common_next_page')}
            >
              {t('lockey_common_next')}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function PageJumpInput({ page, totalPages, onPageChange, ariaLabel }: { page: number; totalPages: number; onPageChange: (p: number) => void; ariaLabel: string }) {
  const [value, setValue] = useState(String(page));

  React.useEffect(() => {
    setValue(String(page));
  }, [page]);

  const commit = () => {
    const parsed = Number(value);
    if (Number.isInteger(parsed) && parsed >= 1 && parsed <= totalPages && parsed !== page) {
      onPageChange(parsed);
    } else {
      setValue(String(page));
    }
  };

  return (
    <input
      type="number"
      min={1}
      max={totalPages}
      value={value}
      onChange={(e) => setValue(e.target.value)}
      onKeyDown={(e) => {
        if (e.key === 'Enter') commit();
      }}
      onBlur={commit}
      className="w-14 rounded-md border border-input bg-background px-2 py-1 text-center text-sm"
      aria-label={ariaLabel}
    />
  );
}
