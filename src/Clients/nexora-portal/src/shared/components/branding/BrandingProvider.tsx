'use client';

import { type ReactNode, useEffect } from 'react';

import { useOrganization } from '@/shared/hooks/useOrganization';

interface BrandingProviderProps {
  children: ReactNode;
}

const ALLOWED_IMAGE_HOSTNAMES = (process.env.NEXT_PUBLIC_ALLOWED_IMAGE_HOSTNAMES ?? 'cdn.nexora.io')
  .split(',')
  .map((h) => h.trim());

function isSafeUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== 'https:') return false;
    return ALLOWED_IMAGE_HOSTNAMES.some(
      (allowed) => parsed.hostname === allowed || parsed.hostname.endsWith(`.${allowed}`),
    );
  } catch {
    return false;
  }
}

/**
 * Applies tenant-specific branding by setting CSS custom properties on :root.
 * This allows tenant logo, colors, and other branding to cascade through
 * the entire portal without prop drilling.
 */
export function BrandingProvider({ children }: BrandingProviderProps) {
  const { organization } = useOrganization();

  useEffect(() => {
    const root = document.documentElement;

    if (organization?.logoUrl && isSafeUrl(organization.logoUrl)) {
      root.style.setProperty('--brand-logo-url', `url(${organization.logoUrl})`);
    } else {
      root.style.removeProperty('--brand-logo-url');
    }

    // Future: apply tenant-specific color overrides from organization settings
    // root.style.setProperty('--brand-primary', tenantColor);

    return () => {
      root.style.removeProperty('--brand-logo-url');
    };
  }, [organization]);

  return <>{children}</>;
}

export { isSafeUrl };
