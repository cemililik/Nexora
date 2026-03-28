import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Mock next-intl
vi.mock('next-intl', () => ({
  useLocale: () => 'en',
  useTranslations: () => (key: string) => key,
}));

// Mock next-auth/react
const mockSignOut = vi.fn();
vi.mock('next-auth/react', () => ({
  signOut: (...args: unknown[]) => mockSignOut(...args),
}));

// Mock i18n navigation
const mockReplace = vi.fn();
vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...props }: Record<string, unknown>) => (
    <a href={href as string} {...props}>
      {children}
    </a>
  ),
  usePathname: () => '/dashboard',
  useRouter: () => ({ replace: mockReplace }),
}));

// Mock i18n routing
vi.mock('@/i18n/routing', () => ({
  routing: { locales: ['en', 'tr'], defaultLocale: 'en' },
}));

// Mock cn utility
vi.mock('@/shared/lib/utils', () => ({
  cn: (...args: unknown[]) => args.filter(Boolean).join(' '),
}));

// Mock authStore
let mockUser: Record<string, unknown> | null = null;
vi.mock('@/shared/lib/stores/authStore', () => ({
  useAuthStore: (selector: (s: Record<string, unknown>) => unknown) =>
    selector({ user: mockUser }),
}));

// Mock uiStore
const mockToggleSidebar = vi.fn();
vi.mock('@/shared/lib/stores/uiStore', () => ({
  useUiStore: (selector: (s: Record<string, unknown>) => unknown) =>
    selector({ toggleSidebar: mockToggleSidebar, sidebarOpen: true }),
}));

// Mock TenantLogo
vi.mock('@/shared/components/branding/TenantLogo', () => ({
  TenantLogo: () => <div data-testid="tenant-logo" />,
}));

import { Topbar } from './Topbar';

describe('Topbar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUser = null;
  });

  it('should render sidebar toggle button', () => {
    render(<Topbar />);

    expect(
      screen.getByLabelText('lockey_common_toggle_sidebar'),
    ).toBeInTheDocument();
  });

  it('should call toggleSidebar when toggle button is clicked', async () => {
    const user = userEvent.setup();

    render(<Topbar />);

    await user.click(screen.getByLabelText('lockey_common_toggle_sidebar'));
    expect(mockToggleSidebar).toHaveBeenCalledTimes(1);
  });

  it('should render TenantLogo', () => {
    render(<Topbar />);

    expect(screen.getByTestId('tenant-logo')).toBeInTheDocument();
  });

  it('should render user name when authenticated', () => {
    mockUser = { firstName: 'John', lastName: 'Doe' };

    render(<Topbar />);

    expect(screen.getByText('John Doe')).toBeInTheDocument();
  });

  it('should not render user info when not authenticated', () => {
    mockUser = null;

    render(<Topbar />);

    expect(screen.queryByText('John Doe')).not.toBeInTheDocument();
  });

  it('should render logout button when authenticated', () => {
    mockUser = { firstName: 'John', lastName: 'Doe' };

    render(<Topbar />);

    expect(
      screen.getByLabelText('lockey_common_logout'),
    ).toBeInTheDocument();
  });

  it('should call signOut when logout button is clicked', async () => {
    const user = userEvent.setup();
    mockUser = { firstName: 'John', lastName: 'Doe' };

    render(<Topbar />);

    await user.click(screen.getByLabelText('lockey_common_logout'));
    expect(mockSignOut).toHaveBeenCalledWith({
      callbackUrl: '/en/auth/login',
    });
  });

  it('should render language switcher with available locales', () => {
    render(<Topbar />);

    const languageSelect = screen.getByLabelText('lockey_common_language');
    expect(languageSelect).toBeInTheDocument();
    expect(languageSelect.tagName).toBe('SELECT');

    const options = languageSelect.querySelectorAll('option');
    expect(options).toHaveLength(2);
  });
});
