'use client';

import { Component, type ErrorInfo, type ReactNode } from 'react';
import { useTranslations } from 'next-intl';

import { reportError } from '@/shared/lib/telemetry';

interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

/**
 * Default fallback UI for ErrorBoundary.
 * Functional component so it can use hooks (useTranslations).
 */
function ErrorBoundaryFallback({ onReset }: { onReset: () => void }) {
  const t = useTranslations();

  return (
    <div className="flex min-h-[400px] flex-col items-center justify-center gap-4 p-8">
      <div className="text-4xl">⚠</div>
      <p className="text-lg font-medium text-foreground">
        {t('lockey_error_something_went_wrong')}
      </p>
      <button
        type="button"
        onClick={onReset}
        className="rounded-md bg-accent px-4 py-2 text-sm text-accent-foreground hover:bg-accent/90"
      >
        {t('lockey_common_try_again')}
      </button>
    </div>
  );
}

/**
 * Error boundary that catches rendering errors and shows a fallback UI.
 * Must be a class component per React requirements.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error('[ErrorBoundary] Render error caught', { error, errorInfo });
    try {
      reportError(error, errorInfo?.componentStack ?? undefined);
    } catch {
      // Telemetry reporting should never throw
    }
  }

  render(): ReactNode {
    if (this.state.hasError) {
      return (
        this.props.fallback ?? (
          <ErrorBoundaryFallback
            onReset={() => this.setState({ hasError: false })}
          />
        )
      );
    }

    return this.props.children;
  }
}
