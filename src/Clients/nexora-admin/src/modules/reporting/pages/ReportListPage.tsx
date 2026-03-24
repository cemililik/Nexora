import { useTranslation } from 'react-i18next';
import { useSearchParams, Link } from 'react-router';

import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { useReportDefinitions } from '../hooks/useReportDefinitions';

export default function ReportListPage() {
  const { t } = useTranslation('reporting');
  const [searchParams, setSearchParams] = useSearchParams();

  const page = Number(searchParams.get('page') ?? '1');
  const search = searchParams.get('search') ?? '';

  const { data, isLoading } = useReportDefinitions({ page, pageSize: 20, search: search || undefined });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-foreground">
          {t('lockey_reporting_nav_reports')}
        </h1>
        <Button asChild>
          <Link to="/reporting/reports/create">
            {t('lockey_reporting_action_create_definition')}
          </Link>
        </Button>
      </div>

      <Input
        placeholder={t('lockey_reporting_search_placeholder')}
        value={search}
        onChange={(e) => {
          const params = new URLSearchParams(searchParams);
          params.set('search', e.target.value);
          params.set('page', '1');
          setSearchParams(params);
        }}
        className="max-w-sm"
      />

      {isLoading && (
        <p className="text-muted-foreground">{t('lockey_reporting_loading')}</p>
      )}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {data?.items.map((def) => (
          <Link key={def.id} to={`/reporting/reports/${def.id}`}>
            <Card className="transition-colors hover:border-primary">
              <CardHeader className="pb-2">
                <CardTitle className="text-base">{def.name}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground line-clamp-2">
                  {def.description ?? t('lockey_reporting_no_description')}
                </p>
                <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
                  <span>{def.module}</span>
                  <span>·</span>
                  <span>{def.defaultFormat}</span>
                  {!def.isActive && (
                    <>
                      <span>·</span>
                      <span className="text-destructive">{t('lockey_reporting_inactive')}</span>
                    </>
                  )}
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>

      {data && data.totalPages > 1 && (
        <div className="flex justify-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={!data.hasPreviousPage}
            onClick={() => {
              const params = new URLSearchParams(searchParams);
              params.set('page', String(page - 1));
              setSearchParams(params);
            }}
          >
            {t('lockey_reporting_prev')}
          </Button>
          <span className="flex items-center text-sm text-muted-foreground">
            {page} / {data.totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={!data.hasNextPage}
            onClick={() => {
              const params = new URLSearchParams(searchParams);
              params.set('page', String(page + 1));
              setSearchParams(params);
            }}
          >
            {t('lockey_reporting_next')}
          </Button>
        </div>
      )}
    </div>
  );
}
