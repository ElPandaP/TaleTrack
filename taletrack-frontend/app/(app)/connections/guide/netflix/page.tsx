import Link from 'next/link';
import { ArrowLeft, Puzzle, Download } from 'lucide-react';
import { getServerDict } from '@/lib/i18n-server';

export default async function NetflixGuidePage() {
  const dict = await getServerDict();
  const t = (key: string) => dict[key] ?? key;
  const steps = [1, 2, 3, 4].map((n) => t(`connections.guide.netflix.step${n}`));

  return (
    <div className="max-w-2xl">
      <Link
        href="/connections"
        className="mb-4 inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="size-4" />
        {t('connections.guide.back')}
      </Link>

      <div className="mb-6 flex items-center gap-2">
        <Puzzle className="size-5 text-primary" />
        <h1 className="font-heading text-2xl font-semibold">{t('connections.netflix.name')}</h1>
      </div>

      <div className="mb-4">
        <a
          href="#"
          className="inline-flex items-center gap-1.5 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm font-medium text-muted-foreground"
        >
          <Download className="size-4" />
          {t('connections.guide.netflix.download')}
        </a>
        <span className="ml-2 text-xs text-muted-foreground/70">
          {t('connections.guide.downloadPending')}
        </span>
      </div>

      <ol className="flex list-decimal flex-col gap-2 pl-5 text-sm text-foreground/90 marker:text-muted-foreground">
        {steps.map((step, i) => (
          <li key={i}>{step}</li>
        ))}
      </ol>

      <p className="mt-3 text-xs text-muted-foreground">{t('connections.guide.netflix.reloadNote')}</p>
    </div>
  );
}
