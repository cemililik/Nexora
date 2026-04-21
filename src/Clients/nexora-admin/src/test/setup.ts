import '@testing-library/jest-dom/vitest';
import React from 'react';
import { vi } from 'vitest';

// Polyfill localStorage / sessionStorage for zustand persist in jsdom.
// Node's jsdom flag parsing can disable the default localStorage with a
// spurious "--localstorage-file was provided" warning, leaving window.localStorage
// as an object without setItem/getItem. We replace it with an in-memory Storage.
function createInMemoryStorage(): Storage {
  const data = new Map<string, string>();
  return {
    get length() { return data.size; },
    clear: () => data.clear(),
    getItem: (k: string) => (data.has(k) ? data.get(k)! : null),
    setItem: (k: string, v: string) => { data.set(k, String(v)); },
    removeItem: (k: string) => { data.delete(k); },
    key: (i: number) => Array.from(data.keys())[i] ?? null,
  };
}
Object.defineProperty(window, 'localStorage', { value: createInMemoryStorage(), configurable: true });
Object.defineProperty(window, 'sessionStorage', { value: createInMemoryStorage(), configurable: true });

// Polyfill DOM methods for Radix UI components in jsdom
if (typeof Element.prototype.hasPointerCapture !== 'function') {
  Element.prototype.hasPointerCapture = () => false;
}
if (typeof Element.prototype.setPointerCapture !== 'function') {
  Element.prototype.setPointerCapture = () => {};
}
if (typeof Element.prototype.releasePointerCapture !== 'function') {
  Element.prototype.releasePointerCapture = () => {};
}
if (typeof Element.prototype.scrollIntoView !== 'function') {
  Element.prototype.scrollIntoView = () => {};
}

// Mock window.matchMedia for Radix UI portal rendering
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

// Mock react-i18next globally for all tests
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string, params?: Record<string, string>) => {
      if (params) {
        return Object.entries(params).reduce(
          (result, [k, v]) => result.replace(`{{${k}}}`, v),
          key,
        );
      }
      return key;
    },
    i18n: {
      language: 'en',
      changeLanguage: vi.fn(),
    },
  }),
  Trans: ({ children }: { children: React.ReactNode }) => children,
  initReactI18next: {},
}));
