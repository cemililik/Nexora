/** Standard API response envelope matching backend ApiEnvelope<T>. */
export interface ApiEnvelope<T> {
  data?: T;
  message?: string;
  meta?: Record<string, string>;
  errors?: ApiValidationError[];
}

/** Field-level validation error from backend FluentValidation. */
export interface ApiValidationError {
  key: string;
  params?: Record<string, string>;
}

/** Paginated response matching backend PagedResult<T>. */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

/** Pagination request parameters. */
export interface PaginationParams {
  page?: number;
  pageSize?: number;
}
