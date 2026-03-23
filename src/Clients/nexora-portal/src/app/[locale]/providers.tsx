'use client';

import { QueryClientProvider } from '@tanstack/react-query';
import { SessionProvider } from 'next-auth/react';
import { type ReactNode, useState } from 'react';
import { Toaster } from 'sonner';

import { BrandingProvider } from '@/shared/components/branding/BrandingProvider';
import { createQueryClient } from '@/shared/lib/query';
import { initTelemetry } from '@/shared/lib/telemetry';

initTelemetry();

interface ProvidersProps {
  children: ReactNode;
}

/** Client-side providers wrapper for the portal. */
export function Providers({ children }: ProvidersProps) {
  const [queryClient] = useState(() => createQueryClient());

  return (
    <SessionProvider>
      <QueryClientProvider client={queryClient}>
        <BrandingProvider>
          {children}
          <Toaster position="top-right" />
        </BrandingProvider>
      </QueryClientProvider>
    </SessionProvider>
  );
}
