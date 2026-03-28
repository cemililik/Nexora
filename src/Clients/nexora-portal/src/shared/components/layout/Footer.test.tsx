import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Mock next-intl
vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) => key,
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

import { Footer } from './Footer';

describe('Footer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSidebarOpen = true;
  });

  it('should render powered by text', () => {
    render(<Footer />);

    expect(screen.getByText('lockey_common_powered_by')).toBeInTheDocument();
  });

  it('should render as a footer element', () => {
    const { container } = render(<Footer />);

    expect(container.querySelector('footer')).toBeInTheDocument();
  });

  it('should render powered by text within a paragraph', () => {
    render(<Footer />);

    const text = screen.getByText('lockey_common_powered_by');
    expect(text.tagName).toBe('P');
  });
});
