import { useTranslation } from 'react-i18next';
import { ChevronRight } from 'lucide-react';
import { Link } from 'react-router';

import { useUiStore } from '@/shared/lib/stores/uiStore';

/** Breadcrumb trail from uiStore state. */
export function Breadcrumbs() {
  const { t } = useTranslation('navigation');
  const breadcrumbs = useUiStore((s) => s.breadcrumbs);

  if (breadcrumbs.length === 0) return null;

  return (
    <nav aria-label="Breadcrumb" className="mb-4 flex items-center gap-1 text-sm text-muted-foreground">
      {breadcrumbs.map((crumb, index) => {
        const isLast = index === breadcrumbs.length - 1;
        return (
          <span key={crumb.label} className="flex items-center gap-1">
            {index > 0 && <ChevronRight className="h-3 w-3" />}
            {crumb.path && !isLast ? (
              <Link to={crumb.path} className="hover:text-foreground">
                {t(crumb.label)}
              </Link>
            ) : (
              <span className={isLast ? 'text-foreground' : undefined}>
                {t(crumb.label)}
              </span>
            )}
          </span>
        );
      })}
    </nav>
  );
}
