import { render, screen } from '@testing-library/react';
import { lazy, type FC } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ModuleSlot } from './ModuleSlot';
import type { PortalModuleManifest } from '@/shared/types/module';

const mockActiveModules: PortalModuleManifest[] = [];
vi.mock('@/shared/hooks/useModules', () => ({
  useModules: () => ({ activeModules: mockActiveModules }),
}));

const mockHasPermission = vi.fn();
vi.mock('@/shared/hooks/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: mockHasPermission }),
}));

vi.mock('@/shared/components/feedback/ErrorBoundary', () => ({
  ErrorBoundary: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

function testComponent(text: string, testId?: string) {
  return lazy(async () => ({
    default: function C() {
      return <div data-testid={testId}>{text}</div>;
    } as FC,
  }));
}

describe('ModuleSlot', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockActiveModules.length = 0;
    mockHasPermission.mockReturnValue(true);
  });

  it('returns null when no contributions match', () => {
    const { container } = render(<ModuleSlot slotId="contact.detail.aside" />);
    expect(container.firstChild).toBeNull();
  });

  it('renders contributions from multiple modules for a slot', async () => {
    mockActiveModules.push(
      {
        name: 'finance',
        navigation: [],
        permissions: [],
        slots: {
          'contact.detail.aside': [
            { id: 'payments', order: 1, permissions: [], component: testComponent('Payments Panel') },
          ],
        },
      },
      {
        name: 'crm',
        navigation: [],
        permissions: [],
        slots: {
          'contact.detail.aside': [
            { id: 'deals', order: 2, permissions: [], component: testComponent('Deals Panel') },
          ],
        },
      },
    );

    render(<ModuleSlot slotId="contact.detail.aside" />);

    expect(await screen.findByText('Payments Panel')).toBeInTheDocument();
    expect(await screen.findByText('Deals Panel')).toBeInTheDocument();
  });

  it('filters contributions by permission', async () => {
    mockHasPermission.mockImplementation((p: string) => p !== 'finance.read');

    mockActiveModules.push({
      name: 'finance',
      navigation: [],
      permissions: [],
      slots: {
        'contact.detail.aside': [
          { id: 'public', order: 1, permissions: [], component: testComponent('Public') },
          { id: 'restricted', order: 2, permissions: ['finance.read'], component: testComponent('Restricted') },
        ],
      },
    });

    render(<ModuleSlot slotId="contact.detail.aside" />);

    expect(await screen.findByText('Public')).toBeInTheDocument();
    expect(screen.queryByText('Restricted')).not.toBeInTheDocument();
  });

  it('sorts contributions by order across modules', async () => {
    mockActiveModules.push(
      {
        name: 'b',
        navigation: [],
        permissions: [],
        slots: {
          'contact.detail.aside': [
            { id: 'second', order: 2, permissions: [], component: testComponent('Second', 'b-second') },
          ],
        },
      },
      {
        name: 'a',
        navigation: [],
        permissions: [],
        slots: {
          'contact.detail.aside': [
            { id: 'first', order: 1, permissions: [], component: testComponent('First', 'a-first') },
          ],
        },
      },
    );

    render(<ModuleSlot slotId="contact.detail.aside" />);

    const first = await screen.findByTestId('a-first');
    const second = await screen.findByTestId('b-second');

    expect(first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('ignores contributions for other slot ids', () => {
    mockActiveModules.push({
      name: 'finance',
      navigation: [],
      permissions: [],
      slots: {
        'other.slot': [
          { id: 'x', order: 1, permissions: [], component: testComponent('Wrong slot') },
        ],
      },
    });

    const { container } = render(<ModuleSlot slotId="contact.detail.aside" />);
    expect(container.firstChild).toBeNull();
  });
});
