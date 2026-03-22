import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

vi.mock('@/shared/lib/i18n', () => ({
  default: {
    t: (key: string) => key,
  },
}));

import { ErrorBoundary } from './ErrorBoundary';

const ThrowingChild = ({ shouldThrow }: { shouldThrow: boolean }) => {
  if (shouldThrow) {
    throw new Error('Test error');
  }
  return <div>Child content</div>;
};

describe('ErrorBoundary', () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it('should render children when no error occurs', () => {
    render(
      <ErrorBoundary>
        <div>Safe content</div>
      </ErrorBoundary>,
    );

    expect(screen.getByText('Safe content')).toBeInTheDocument();
  });

  it('should render default fallback UI when child throws', () => {
    render(
      <ErrorBoundary>
        <ThrowingChild shouldThrow />
      </ErrorBoundary>,
    );

    expect(screen.queryByText('Child content')).not.toBeInTheDocument();
    expect(
      screen.getByText('lockey_error_something_went_wrong'),
    ).toBeInTheDocument();
    expect(screen.getByText('lockey_common_try_again')).toBeInTheDocument();
  });

  it('should render custom fallback when provided and child throws', () => {
    render(
      <ErrorBoundary fallback={<div>Custom error view</div>}>
        <ThrowingChild shouldThrow />
      </ErrorBoundary>,
    );

    expect(screen.queryByText('Child content')).not.toBeInTheDocument();
    expect(screen.getByText('Custom error view')).toBeInTheDocument();
  });

  it('should display translated error message using i18n keys', () => {
    render(
      <ErrorBoundary>
        <ThrowingChild shouldThrow />
      </ErrorBoundary>,
    );

    expect(
      screen.getByText('lockey_error_something_went_wrong'),
    ).toBeInTheDocument();
    expect(screen.getByText('lockey_common_try_again')).toBeInTheDocument();
  });

  it('should reset error state and re-render children when retry button is clicked', async () => {
    const user = userEvent.setup();

    let shouldThrow = true;

    const ConditionalChild = () => {
      if (shouldThrow) {
        throw new Error('Test error');
      }
      return <div>Recovered content</div>;
    };

    const { rerender } = render(
      <ErrorBoundary>
        <ConditionalChild />
      </ErrorBoundary>,
    );

    expect(
      screen.getByText('lockey_error_something_went_wrong'),
    ).toBeInTheDocument();

    // Fix the child so it no longer throws on next render
    shouldThrow = false;

    await user.click(screen.getByText('lockey_common_try_again'));

    // Force rerender so the boundary picks up the fixed child
    rerender(
      <ErrorBoundary>
        <ConditionalChild />
      </ErrorBoundary>,
    );

    expect(screen.getByText('Recovered content')).toBeInTheDocument();
    expect(
      screen.queryByText('lockey_error_something_went_wrong'),
    ).not.toBeInTheDocument();
  });
});
