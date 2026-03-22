import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import '@/shared/lib/i18n';
import '@/app/globals.css';

import { App } from '@/app/App';

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error('Root element not found');
}

createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
