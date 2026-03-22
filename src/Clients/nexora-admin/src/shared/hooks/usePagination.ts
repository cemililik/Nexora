import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router';

/**
 * Hook for URL-based pagination state.
 * Reads page and pageSize from URL search params.
 */
export function usePagination(defaultPageSize = 20) {
  const [searchParams, setSearchParams] = useSearchParams();

  const page = useMemo(() => {
    const raw = Number(searchParams.get('page'));
    return Number.isNaN(raw) || raw <= 0 ? 1 : raw;
  }, [searchParams]);

  const pageSize = useMemo(() => {
    const raw = Number(searchParams.get('pageSize'));
    return Number.isNaN(raw) || raw <= 0 ? defaultPageSize : raw;
  }, [searchParams, defaultPageSize]);

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
