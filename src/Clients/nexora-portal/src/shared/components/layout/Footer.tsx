'use client';

import { useTranslations } from 'next-intl';

import { cn } from '@/shared/lib/utils';
import { useUiStore } from '@/shared/lib/stores/uiStore';

export function Footer() {
  const t = useTranslations();
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);

  return (
    <footer
      className={cn(
        'border-t border-border bg-background px-6 py-4 text-center text-sm text-muted-foreground transition-all duration-300',
        sidebarOpen ? 'ml-64' : 'ml-16',
      )}
    >
      <p>{t('lockey_common_powered_by')}</p>
    </footer>
  );
}
