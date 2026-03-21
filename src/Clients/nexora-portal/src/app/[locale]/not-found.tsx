import { useTranslations } from 'next-intl';

export default function NotFoundPage() {
  const t = useTranslations();

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-6xl font-bold text-muted-foreground">404</h1>
      <p className="text-lg text-muted-foreground">
        {t('lockey_error_not_found')}
      </p>
    </div>
  );
}
