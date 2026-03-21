import { render, screen } from '@testing-library/react';
import { lazy, type FC } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { SectionRenderer } from './SectionRenderer';
import type { PortalModuleManifest } from '@/shared/types/module';

// Mock useModules
const mockActiveModules: PortalModuleManifest[] = [];
vi.mock('@/shared/hooks/useModules', () => ({
  useModules: () => ({ activeModules: mockActiveModules }),
}));

// Mock usePermissions
const mockHasPermission = vi.fn();
vi.mock('@/shared/hooks/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: mockHasPermission }),
}));

// Mock ErrorBoundary to pass-through
vi.mock('@/shared/components/feedback/ErrorBoundary', () => ({
  ErrorBoundary: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

function createTestComponent(text: string) {
  return lazy(
    async () => ({
      default: function TestComponent() {
        return <div>{text}</div>;
      } as FC,
    }),
  );
}

describe('SectionRenderer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockActiveModules.length = 0;
    mockHasPermission.mockReturnValue(true);
  });

  it('should return null when no sections match the position', () => {
    const { container } = render(
      <SectionRenderer position="dashboard-main" />,
    );

    expect(container.firstChild).toBeNull();
  });

  it('should filter sections by position', async () => {
    mockActiveModules.push({
      name: 'donations',
      navigation: [],
      permissions: ['donations.read'],
      sections: [
        {
          id: 'donation-stats',
          position: 'dashboard-main',
          order: 1,
          component: createTestComponent('Donation Stats'),
          permissions: [],
        },
        {
          id: 'donation-sidebar',
          position: 'dashboard-sidebar',
          order: 1,
          component: createTestComponent('Sidebar Widget'),
          permissions: [],
        },
      ],
    });

    render(<SectionRenderer position="dashboard-main" />);

    expect(
      await screen.findByText('Donation Stats'),
    ).toBeInTheDocument();
    expect(screen.queryByText('Sidebar Widget')).not.toBeInTheDocument();
  });

  it('should filter sections by permission', async () => {
    mockHasPermission.mockImplementation(
      (p: string) => p !== 'restricted.read',
    );

    mockActiveModules.push({
      name: 'donations',
      navigation: [],
      permissions: ['donations.read'],
      sections: [
        {
          id: 'public-section',
          position: 'dashboard-main',
          order: 1,
          component: createTestComponent('Public Section'),
          permissions: [],
        },
        {
          id: 'restricted-section',
          position: 'dashboard-main',
          order: 2,
          component: createTestComponent('Restricted Section'),
          permissions: ['restricted.read'],
        },
      ],
    });

    render(<SectionRenderer position="dashboard-main" />);

    expect(
      await screen.findByText('Public Section'),
    ).toBeInTheDocument();
    expect(
      screen.queryByText('Restricted Section'),
    ).not.toBeInTheDocument();
  });

  it('should sort sections by order', async () => {
    mockActiveModules.push({
      name: 'donations',
      navigation: [],
      permissions: ['donations.read'],
      sections: [
        {
          id: 'second',
          position: 'dashboard-main',
          order: 2,
          component: createTestComponent('Second'),
          permissions: [],
        },
        {
          id: 'first',
          position: 'dashboard-main',
          order: 1,
          component: createTestComponent('First'),
          permissions: [],
        },
      ],
    });

    render(<SectionRenderer position="dashboard-main" />);

    const first = await screen.findByText('First');
    const second = await screen.findByText('Second');

    // Check DOM order
    const parent = first.closest('div')!.parentElement!;
    const children = Array.from(parent.children);
    const firstIdx = children.findIndex((c) =>
      c.textContent?.includes('First'),
    );
    const secondIdx = children.findIndex((c) =>
      c.textContent?.includes('Second'),
    );
    expect(firstIdx).toBeLessThan(secondIdx);
  });
});
