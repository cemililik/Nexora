import { Component, type ErrorInfo, type ReactNode } from 'react';

import i18n from '@/shared/lib/i18n';
import { reportError } from '@/shared/lib/telemetry';

interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

/**
 * Error boundary component that catches rendering errors.
 * Must be a class component (React requirement).
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
    console.error('[ErrorBoundary]', error, errorInfo);
    try {
      reportError(error, errorInfo?.componentStack ?? undefined);
    } catch {
      // Telemetry reporting should never throw
    }
  }

  render(): ReactNode {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <div className="flex min-h-[200px] flex-col items-center justify-center gap-4 p-8">
          <p className="text-muted-foreground">
            {i18n.t('lockey_error_something_went_wrong')}
          </p>
          <button
            type="button"
            onClick={() => this.setState({ hasError: false })}
            className="rounded-md bg-primary px-4 py-2 text-sm text-primary-foreground hover:bg-primary/90"
          >
            {i18n.t('lockey_common_try_again')}
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
