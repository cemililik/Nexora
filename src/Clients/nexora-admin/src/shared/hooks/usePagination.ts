import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router';

/**
 * Hook for URL-based pagination state.
 * Reads page and pageSize from URL search params.
 */
export function usePagination(defaultPageSize = 20) {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = useMemo(
    () => Number(searchParams.get('page')) || 1,
    [searchParams],
  );

  const pageSize = useMemo(
    () => Number(searchParams.get('pageSize')) || defaultPageSize,
    [searchParams, defaultPageSize],
  );

  const setPage = useCallback(
    (newPage: number) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.set('page', String(newPage));
        return next;
      });
    },
    [setSearchParams],
  );

  const setPageSize = useCallback(
    (newPageSize: number) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.set('pageSize', String(newPageSize));
        next.set('page', '1');
        return next;
      });
    },
    [setSearchParams],
  );

  return { page, pageSize, setPage, setPageSize };
}
