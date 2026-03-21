import { getTranslations } from 'next-intl/server';

export default async function NotFoundPage() {
  let notFoundText = 'Page not found';
  try {
    const t = await getTranslations();
    notFoundText = t('lockey_error_not_found');
  } catch {
    // Locale context not available for root-level 404s — use fallback
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-6xl font-bold text-muted-foreground">404</h1>
      <p className="text-lg text-muted-foreground">
        {notFoundText}
      </p>
    </div>
  );
}
