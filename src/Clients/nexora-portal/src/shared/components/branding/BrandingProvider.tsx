'use client';

import { type ReactNode, useEffect } from 'react';

import { useOrganization } from '@/shared/hooks/useOrganization';

interface BrandingProviderProps {
  children: ReactNode;
}

/**
 * Applies tenant-specific branding by setting CSS custom properties on :root.
 * This allows tenant logo, colors, and other branding to cascade through
 * the entire portal without prop drilling.
 */
export function BrandingProvider({ children }: BrandingProviderProps) {
  const { organization } = useOrganization();

  useEffect(() => {
    if (!organization) return;

    const root = document.documentElement;

    if (organization.logoUrl) {
      root.style.setProperty('--brand-logo-url', `url(${organization.logoUrl})`);
    }

    // Future: apply tenant-specific color overrides from organization settings
    // root.style.setProperty('--brand-primary', tenantColor);
  }, [organization]);

  return <>{children}</>;
}
