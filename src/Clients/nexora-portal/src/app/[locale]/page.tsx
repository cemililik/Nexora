import { redirect } from '@/i18n/navigation';

/**
 * Portal root page — redirects to dashboard for authenticated users.
 * Unauthenticated users will be caught by middleware and sent to login.
 */
export default async function HomePage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  redirect({ href: '/dashboard', locale });
}
