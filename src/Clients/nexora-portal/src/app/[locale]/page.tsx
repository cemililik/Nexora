import { redirect } from '@/i18n/navigation';

/**
 * Portal root page — redirects to dashboard for authenticated users.
 * Unauthenticated users will be caught by middleware and sent to login.
 */
export default function HomePage() {
  redirect({ href: '/dashboard', locale: 'en' });
}
