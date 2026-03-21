import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { RequireAuth } from './RequireAuth';

// Mock next-auth/react
const mockUseSession = vi.fn();
vi.mock('next-auth/react', () => ({
  useSession: () => mockUseSession(),
}));

// Mock i18n navigation
const mockReplace = vi.fn();
vi.mock('@/i18n/navigation', () => ({
  useRouter: () => ({ replace: mockReplace }),
}));

describe('RequireAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should show loading spinner when session is loading', () => {
    mockUseSession.mockReturnValue({ status: 'loading' });

    const { container } = render(
      <RequireAuth>
        <div>Protected Content</div>
      </RequireAuth>,
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    expect(container.querySelector('.animate-spin')).toBeInTheDocument();
  });

  it('should render children when authenticated', () => {
    mockUseSession.mockReturnValue({
      status: 'authenticated',
      data: { user: { name: 'Test' } },
    });

    render(
      <RequireAuth>
        <div>Protected Content</div>
      </RequireAuth>,
    );

    expect(screen.getByText('Protected Content')).toBeInTheDocument();
  });

  it('should redirect to login when unauthenticated', () => {
    mockUseSession.mockReturnValue({ status: 'unauthenticated' });

    render(
      <RequireAuth>
        <div>Protected Content</div>
      </RequireAuth>,
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    expect(mockReplace).toHaveBeenCalledWith('/auth/login');
  });

  it('should render custom fallback when loading', () => {
    mockUseSession.mockReturnValue({ status: 'loading' });

    render(
      <RequireAuth fallback={<div>Custom Loading</div>}>
        <div>Protected Content</div>
      </RequireAuth>,
    );

    expect(screen.getByText('Custom Loading')).toBeInTheDocument();
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
  });
});
