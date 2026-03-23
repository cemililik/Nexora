import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** Merge Tailwind CSS classes with conditional support. */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

/** UUID v4 validation regex. */
export const UUID_REGEX =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Convert a PascalCase or camelCase string to snake_case. */
export const toSnakeCase = (str: string): string =>
  str.replace(/([A-Z])/g, '_$1').toLowerCase().replace(/^_/, '');
