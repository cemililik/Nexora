import '@testing-library/jest-dom/vitest';
import { vi } from 'vitest';

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
}));
