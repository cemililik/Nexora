import type { ReactNode } from 'react';

/**
 * Root layout — delegates html/body structure to [locale]/layout.tsx.
 * This is intentional: next-intl requires locale-aware html/body tags
 * (lang and dir attributes depend on the locale segment).
 * See: https://next-intl-docs.vercel.app/docs/getting-started/app-router
 */
export default function RootLayout({ children }: { children: ReactNode }) {
  return children;
}
