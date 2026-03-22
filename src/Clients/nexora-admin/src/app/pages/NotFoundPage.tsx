import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { Button } from '@/shared/components/ui/button';

/** 404 page for unmatched routes. */
export default function NotFoundPage() {
  const { t } = useTranslation('common');

  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4">
      <h1 className="text-4xl font-bold">{t('lockey_common_not_found_title')}</h1>
      <p className="text-muted-foreground">
        {t('lockey_common_not_found_description')}
      </p>
      <Button type="button" asChild>
        <Link to="/dashboard">{t('lockey_common_go_to_dashboard')}</Link>
      </Button>
    </div>
  );
}
