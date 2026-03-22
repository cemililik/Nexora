import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';

let mockIsAuthenticated = false;
let mockToken: string | null = null;
vi.mock('@/shared/lib/stores/authStore', () => ({
  useAuthStore: (selector: (s: { isAuthenticated: boolean; token: string | null }) => unknown) =>
    selector({ isAuthenticated: mockIsAuthenticated, token: mockToken }),
}));

import { RequireAuth } from './RequireAuth';

describe('RequireAuth', () => {
  beforeEach(() => {
    mockIsAuthenticated = false;
    mockToken = null;
  });

  it('should render children when authenticated', () => {
    mockIsAuthenticated = true;
    mockToken = 'valid-token';

    render(
      <MemoryRouter>
        <RequireAuth>
          <div>Protected Content</div>
        </RequireAuth>
      </MemoryRouter>,
    );

    expect(screen.getByText('Protected Content')).toBeInTheDocument();
  });

  it('should redirect to login when not authenticated', () => {
    mockIsAuthenticated = false;

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route
            path="/dashboard"
            element={
              <RequireAuth>
                <div>Protected Content</div>
              </RequireAuth>
            }
          />
          <Route path="/login" element={<div>Login Page</div>} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    expect(screen.getByText('Login Page')).toBeInTheDocument();
  });

  it('should show fallback when not authenticated and fallback provided', () => {
    mockIsAuthenticated = false;

    render(
      <MemoryRouter>
        <RequireAuth fallback={<div>Loading...</div>}>
          <div>Protected Content</div>
        </RequireAuth>
      </MemoryRouter>,
    );

    expect(screen.getByText('Loading...')).toBeInTheDocument();
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
  });

  it('should redirect when token is null even if isAuthenticated is true', () => {
    mockIsAuthenticated = true;
    mockToken = null;

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route
            path="/dashboard"
            element={
              <RequireAuth>
                <div>Protected Content</div>
              </RequireAuth>
            }
          />
          <Route path="/login" element={<div>Login Page</div>} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    expect(screen.getByText('Login Page')).toBeInTheDocument();
  });
});
