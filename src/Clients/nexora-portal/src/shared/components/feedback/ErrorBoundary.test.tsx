import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Mock next-intl
vi.mock('next-intl', () => ({
  useTranslations: () => (key: string) => key,
}));

// Mock telemetry
vi.mock('@/shared/lib/telemetry', () => ({
  reportError: vi.fn(),
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

  it('should show retry button in default fallback', () => {
    render(
      <ErrorBoundary>
        <ThrowingChild shouldThrow />
      </ErrorBoundary>,
    );

    const retryButton = screen.getByText('lockey_common_try_again');
    expect(retryButton.tagName).toBe('BUTTON');
  });

  it('should reset error state when retry button is clicked', async () => {
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

    shouldThrow = false;

    await user.click(screen.getByText('lockey_common_try_again'));

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

  it('should call reportError when child throws', async () => {
    const { reportError } = await import('@/shared/lib/telemetry');

    render(
      <ErrorBoundary>
        <ThrowingChild shouldThrow />
      </ErrorBoundary>,
    );

    expect(reportError).toHaveBeenCalled();
  });
});
