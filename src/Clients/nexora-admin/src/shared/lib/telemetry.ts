import { trace } from '@opentelemetry/api';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions';
import { SimpleSpanProcessor, WebTracerProvider } from '@opentelemetry/sdk-trace-web';

const SERVICE_NAME = 'nexora-admin';

let initialized = false;

/**
 * Initializes OpenTelemetry tracing for frontend error reporting.
 * Uses SimpleSpanProcessor — acceptable for low-volume error-only reporting.
 */
export function initTelemetry(): void {
  if (initialized) return;

  try {
    const endpoint = import.meta.env.VITE_OTEL_ENDPOINT ?? 'http://localhost:4328';

    const exporter = new OTLPTraceExporter({
      url: `${endpoint}/v1/traces`,
    });

    const provider = new WebTracerProvider({
      resource: resourceFromAttributes({ [ATTR_SERVICE_NAME]: SERVICE_NAME }),
      spanProcessors: [new SimpleSpanProcessor(exporter)],
    });

    provider.register();
    initialized = true;
  } catch (err) {
    console.error('[Telemetry] initTelemetry failed:', err);
  }
}

/**
 * Reports a caught error as an OpenTelemetry span with exception details.
 * Intended for use in ErrorBoundary componentDidCatch.
 */
export function reportError(error: Error, componentStack?: string): void {
  const tracer = trace.getTracer(SERVICE_NAME);
  const span = tracer.startSpan('error-boundary-catch');
  span.setAttribute('error.type', error.name);
  span.setAttribute('error.message', error.message);
  if (error.stack) span.setAttribute('error.stack', error.stack);
  if (componentStack) span.setAttribute('error.component_stack', componentStack);
  span.recordException(error);
  span.end();
}
