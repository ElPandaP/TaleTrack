import Link from 'next/link';
import { ArrowLeft, BookOpen, Download, ExternalLink } from 'lucide-react';
import { getServerDict } from '@/lib/i18n-server';

export default async function KoreaderGuidePage() {
  const dict = await getServerDict();
  const t = (key: string) => dict[key] ?? key;
  const steps = [1, 2, 3, 4].map((n) => t(`connections.guide.koreader.step${n}`));

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
        <BookOpen className="size-5 text-primary" />
        <h1 className="font-heading text-2xl font-semibold">{t('connections.koreader.name')}</h1>
      </div>

      <div className="mb-4">
        <a
          href="#"
          className="inline-flex items-center gap-1.5 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm font-medium text-muted-foreground"
        >
          <Download className="size-4" />
          {t('connections.guide.koreader.download')}
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

      <a
        href="https://github.com/koreader/koreader/wiki/Installation"
        target="_blank"
        rel="noopener noreferrer"
        className="mt-3 inline-flex items-center gap-1.5 text-sm text-primary hover:underline"
      >
        {t('connections.guide.koreader.officialGuide')}
        <ExternalLink className="size-3.5" />
      </a>
    </div>
  );
}
