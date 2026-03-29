import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

export interface ColumnDef<T> {
  key: string;
  header: ReactNode;
  render: (row: T) => ReactNode;
  className?: string;
}

interface DataTableProps<T> {
  columns: ColumnDef<T>[];
  data: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
  isLoading?: boolean;
  emptyMessage?: string;
  keyExtractor?: (row: T, index: number) => string | number;
  onRowClick?: (row: T) => void;
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
  isLoading = false,
  emptyMessage,
  keyExtractor = (_row, index) => index,
  onRowClick,
}: DataTableProps<T>) {
  const { t } = useTranslation();
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
        <div className="flex items-center justify-center p-12 text-muted-foreground">
          {emptyMessage ?? t('lockey_common_no_results')}
        </div>
      ) : (
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/50">
              <tr>
                {columns.map((col) => (
                  <th
                    key={col.key}
                    className="px-4 py-3 text-start font-medium text-muted-foreground"
                  >
                    {col.header}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.map((row, index) => (
                <tr
                  key={keyExtractor(row, index)}
                  className={`border-b last:border-0${onRowClick ? ' cursor-pointer hover:bg-muted/50 transition-colors' : ''}`}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                >
                  {columns.map((col) => (
                    <td key={col.key} className={col.className ?? 'px-4 py-3'}>
                      {col.render(row)}
                    </td>
                  ))}
                </tr>
              ))}
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
                <label htmlFor="dt-page-size" className="text-sm text-muted-foreground">
                  {t('lockey_common_items_per_page')}
                </label>
                <select
                  id="dt-page-size"
                  value={pageSize}
                  onChange={(e) => onPageSizeChange(Number(e.target.value))}
                  className="rounded-md border border-input bg-background px-2 py-1 text-sm"
                >
                  {[20, 50, 100].map((size) => (
                    <option key={size} value={size}>
                      {size}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => onPageChange(page - 1)}
            >
              {t('lockey_common_previous')}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={page >= totalPages}
              onClick={() => onPageChange(page + 1)}
            >
              {t('lockey_common_next')}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
