import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Mock useOrganization
const mockOrganization = vi.fn();
vi.mock('@/shared/hooks/useOrganization', () => ({
  useOrganization: () => ({ organization: mockOrganization() }),
}));

import { BrandingProvider, isSafeUrl } from './BrandingProvider';

describe('isSafeUrl', () => {
  it('should accept HTTPS URL on allowed hostname', () => {
    expect(isSafeUrl('https://cdn.nexora.io/logos/acme.png')).toBe(true);
  });

  it('should accept HTTPS URL on subdomain of allowed hostname', () => {
    expect(isSafeUrl('https://images.cdn.nexora.io/logos/acme.png')).toBe(true);
  });

  it('should reject HTTP URLs', () => {
    expect(isSafeUrl('http://cdn.nexora.io/logos/acme.png')).toBe(false);
  });

  it('should reject URLs on non-allowed hostnames', () => {
    expect(isSafeUrl('https://evil.example.com/logo.png')).toBe(false);
  });

  it('should reject invalid URL strings', () => {
    expect(isSafeUrl('not-a-url')).toBe(false);
  });

  it('should reject empty string', () => {
    expect(isSafeUrl('')).toBe(false);
  });

  it('should reject javascript protocol', () => {
    expect(isSafeUrl('javascript:alert(1)')).toBe(false);
  });

  it('should reject data URIs', () => {
    expect(isSafeUrl('data:image/png;base64,abc')).toBe(false);
  });
});

describe('BrandingProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    document.documentElement.style.removeProperty('--brand-logo-url');
  });

  it('should render children', () => {
    mockOrganization.mockReturnValue(null);

    render(
      <BrandingProvider>
        <div>Child content</div>
      </BrandingProvider>,
    );

    expect(screen.getByText('Child content')).toBeInTheDocument();
  });

  it('should set CSS custom property when organization has a safe logo URL', () => {
    mockOrganization.mockReturnValue({
      name: 'Acme Corp',
      logoUrl: 'https://cdn.nexora.io/logos/acme.png',
    });

    render(
      <BrandingProvider>
        <div>Child</div>
      </BrandingProvider>,
    );

    expect(
      document.documentElement.style.getPropertyValue('--brand-logo-url'),
    ).toBe('url(https://cdn.nexora.io/logos/acme.png)');
  });

  it('should not set CSS custom property when logo URL is unsafe', () => {
    mockOrganization.mockReturnValue({
      name: 'Acme Corp',
      logoUrl: 'http://evil.example.com/logo.png',
    });

    render(
      <BrandingProvider>
        <div>Child</div>
      </BrandingProvider>,
    );

    expect(
      document.documentElement.style.getPropertyValue('--brand-logo-url'),
    ).toBe('');
  });

  it('should not set CSS custom property when organization has no logo', () => {
    mockOrganization.mockReturnValue({
      name: 'Acme Corp',
      logoUrl: null,
    });

    render(
      <BrandingProvider>
        <div>Child</div>
      </BrandingProvider>,
    );

    expect(
      document.documentElement.style.getPropertyValue('--brand-logo-url'),
    ).toBe('');
  });
});
