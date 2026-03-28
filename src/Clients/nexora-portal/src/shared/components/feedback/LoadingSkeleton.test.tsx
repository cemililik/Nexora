import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

// Mock cn utility
vi.mock('@/shared/lib/utils', () => ({
  cn: (...args: unknown[]) => args.filter(Boolean).join(' '),
}));

import { LoadingSkeleton } from './LoadingSkeleton';

describe('LoadingSkeleton', () => {
  it('should have role="status" for accessibility', () => {
    render(<LoadingSkeleton />);

    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('should have aria-label "Loading"', () => {
    render(<LoadingSkeleton />);

    expect(screen.getByLabelText('Loading')).toBeInTheDocument();
  });

  it('should apply animate-pulse class', () => {
    render(<LoadingSkeleton />);

    const skeleton = screen.getByRole('status');
    expect(skeleton.className).toContain('animate-pulse');
  });

  it('should accept custom className', () => {
    render(<LoadingSkeleton className="mt-8" />);

    const skeleton = screen.getByRole('status');
    expect(skeleton.className).toContain('mt-8');
  });

  it('should render skeleton placeholder elements', () => {
    const { container } = render(<LoadingSkeleton />);

    // Should have multiple bg-muted placeholder divs
    const placeholders = container.querySelectorAll('.bg-muted');
    expect(placeholders.length).toBeGreaterThanOrEqual(3);
  });
});
