import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';

export interface ColumnDef<T> {
  id: string;
  header: string;
  accessor: (row: T) => ReactNode;
  className?: string;
}

interface DataTableProps<T> {
  columns: ColumnDef<T>[];
  data: T[];
  keyExtractor: (row: T) => string;
  isLoading?: boolean;
  page?: number;
  totalPages?: number;
  onPageChange?: (page: number) => void;
}

/** Generic data table with pagination and loading state. */
export function DataTable<T>({
  columns,
  data,
  keyExtractor,
  isLoading = false,
  page = 1,
  totalPages = 1,
  onPageChange,
}: DataTableProps<T>) {
  const { t } = useTranslation();

  if (isLoading) {
    return (
      <div role="status" aria-label="Loading" className="space-y-2">
        {Array.from({ length: 5 }, (_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        {t('lockey_common_no_results')}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="overflow-x-auto rounded-md border">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/50">
            <tr>
              {columns.map((col) => (
                <th
                  key={col.id}
                  className="px-4 py-3 text-start font-medium text-muted-foreground"
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.map((row) => (
              <tr key={keyExtractor(row)} className="border-b last:border-0">
                {columns.map((col) => (
                  <td key={col.id} className={col.className ?? 'px-4 py-3'}>
                    {col.accessor(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && onPageChange && (
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">
            {t('lockey_common_page_of', {
              page: String(page),
              totalPages: String(totalPages),
            })}
          </span>
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
