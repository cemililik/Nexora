import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useRef, type ReactNode } from 'react';

import { createQueryClient } from '@/shared/lib/query';

interface QueryProviderProps {
  children: ReactNode;
}

/** QueryClient provider with factory pattern (per ARCH-16). */
export function QueryProvider({ children }: QueryProviderProps) {
  const queryClientRef = useRef<QueryClient | null>(null);
  if (!queryClientRef.current) {
    queryClientRef.current = createQueryClient();
  }

  return (
    <QueryClientProvider client={queryClientRef.current}>
      {children}
    </QueryClientProvider>
  );
}
