import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Mock next-intl
vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) => key,
}));

// Mock i18n navigation
let mockPathname = '/dashboard';

interface MockLinkProps {
  children: React.ReactNode;
  href: string;
  'aria-current'?: string;
  className?: string;
}

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...props }: MockLinkProps) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
  usePathname: () => mockPathname,
}));

// Mock cn utility
vi.mock('@/shared/lib/utils', () => ({
  cn: (...args: unknown[]) => args.filter(Boolean).join(' '),
}));

// Mock uiStore
let mockSidebarOpen = true;
vi.mock('@/shared/lib/stores/uiStore', () => ({
  useUiStore: (selector: (s: Record<string, unknown>) => unknown) =>
    selector({ sidebarOpen: mockSidebarOpen }),
}));

// Mock useModules
const mockActiveModules = vi.fn();
vi.mock('@/shared/hooks/useModules', () => ({
  useModules: () => ({ activeModules: mockActiveModules() }),
}));

// Mock usePermissions
const mockHasPermission = vi.fn();
vi.mock('@/shared/hooks/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: mockHasPermission }),
}));

import { Sidebar } from './Sidebar';

const donationsModuleFixture = {
  name: 'donations',
  navigation: [
    { label: 'lockey_nav_donations', path: '/donations', icon: 'Heart' },
  ],
  permissions: ['donations.read'],
  sections: [],
};

describe('Sidebar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPathname = '/dashboard';
    mockSidebarOpen = true;
    mockActiveModules.mockReturnValue([]);
    mockHasPermission.mockReturnValue(true);
  });

  it('should render dashboard navigation link', () => {
    render(<Sidebar />);

    const dashboardLink = screen.getByRole('link', { name: /lockey_nav_dashboard|dashboard/i });
    expect(dashboardLink).toHaveAttribute('href', '/dashboard');
  });

  it('should set aria-current="page" on active navigation item', () => {
    mockPathname = '/dashboard';

    render(<Sidebar />);

    const dashboardLink = screen.getByRole('link', { current: 'page' });
    expect(dashboardLink).toHaveAttribute('href', '/dashboard');
  });

  it('should not set aria-current on inactive navigation items', () => {
    mockPathname = '/dashboard';
    mockActiveModules.mockReturnValue([donationsModuleFixture]);

    render(<Sidebar />);

    const donationsLink = screen.getByRole('link', { name: /lockey_nav_donations/i });
    expect(donationsLink).not.toHaveAttribute('aria-current');
  });

  it('should render module navigation items when user has permission', () => {
    mockActiveModules.mockReturnValue([donationsModuleFixture]);
    mockHasPermission.mockReturnValue(true);

    render(<Sidebar />);

    expect(screen.getByText('lockey_nav_donations')).toBeInTheDocument();
  });

  it('should hide module navigation when user lacks permission', () => {
    mockActiveModules.mockReturnValue([donationsModuleFixture]);
    mockHasPermission.mockReturnValue(false);

    render(<Sidebar />);

    expect(screen.queryByText('lockey_nav_donations')).not.toBeInTheDocument();
  });

  it('should show app name when sidebar is open', () => {
    mockSidebarOpen = true;

    render(<Sidebar />);

    expect(screen.getByText('lockey_common_app_name')).toBeInTheDocument();
  });

  it('should hide app name when sidebar is closed', () => {
    mockSidebarOpen = false;

    render(<Sidebar />);

    expect(screen.queryByText('lockey_common_app_name')).not.toBeInTheDocument();
  });
});
