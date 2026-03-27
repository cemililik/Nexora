import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Mock next/image to render a standard img element
vi.mock('next/image', () => ({
  default: (props: Record<string, unknown>) => (
    // eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text
    <img {...props} />
  ),
}));

// Mock useOrganization
const mockOrganization = vi.fn();
vi.mock('@/shared/hooks/useOrganization', () => ({
  useOrganization: () => ({ organization: mockOrganization() }),
}));

// Mock isSafeUrl
const mockIsSafeUrl = vi.fn();
vi.mock('./BrandingProvider', () => ({
  isSafeUrl: (url: string) => mockIsSafeUrl(url),
}));

// Mock cn utility
vi.mock('@/shared/lib/utils', () => ({
  cn: (...args: unknown[]) => args.filter(Boolean).join(' '),
}));

import { TenantLogo } from './TenantLogo';

describe('TenantLogo', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render loading skeleton when organization is null', () => {
    mockOrganization.mockReturnValue(null);

    const { container } = render(<TenantLogo />);

    const skeleton = container.firstElementChild;
    expect(skeleton).toBeInTheDocument();
    expect(skeleton?.className).toContain('animate-pulse');
  });

  it('should render image when organization has a safe logo URL', () => {
    mockOrganization.mockReturnValue({
      name: 'Acme Corp',
      logoUrl: 'https://cdn.nexora.io/logos/acme.png',
    });
    mockIsSafeUrl.mockReturnValue(true);

    render(<TenantLogo size={48} />);

    const img = screen.getByRole('img', { name: 'Acme Corp' });
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute('src', 'https://cdn.nexora.io/logos/acme.png');
    expect(img).toHaveAttribute('width', '48');
    expect(img).toHaveAttribute('height', '48');
  });

  it('should render initials fallback when logo URL is unsafe', () => {
    mockOrganization.mockReturnValue({
      name: 'Acme Corp',
      logoUrl: 'http://evil.example.com/logo.png',
    });
    mockIsSafeUrl.mockReturnValue(false);

    render(<TenantLogo />);

    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(screen.getByText('AC')).toBeInTheDocument();
  });

  it('should render initials fallback when no logo URL', () => {
    mockOrganization.mockReturnValue({
      name: 'Nexora Foundation',
      logoUrl: null,
    });

    render(<TenantLogo />);

    expect(screen.getByText('NF')).toBeInTheDocument();
  });

  it('should truncate initials to two characters', () => {
    mockOrganization.mockReturnValue({
      name: 'The Very Long Organization Name',
      logoUrl: null,
    });

    render(<TenantLogo />);

    expect(screen.getByText('TV')).toBeInTheDocument();
  });

  it('should use default size of 32', () => {
    mockOrganization.mockReturnValue({
      name: 'Acme',
      logoUrl: null,
    });

    const { container } = render(<TenantLogo />);

    const div = container.firstElementChild as HTMLElement;
    expect(div.style.width).toBe('32px');
    expect(div.style.height).toBe('32px');
  });
});
