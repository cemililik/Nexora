import type { ReactNode } from 'react';

/**
 * Root layout — minimal wrapper that delegates to [locale]/layout.tsx.
 * Required by Next.js App Router as the outermost layout.
 */
export default function RootLayout({ children }: { children: ReactNode }) {
  return children;
}
