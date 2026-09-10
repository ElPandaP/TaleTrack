import Link from 'next/link';
import { ArrowLeft, Puzzle, BookOpen, Download, ExternalLink } from 'lucide-react';
import { getServerDict } from '@/lib/i18n-server';

export default async function ConnectionsGuidePage() {
  const dict = await getServerDict();
  const t = (key: string) => dict[key] ?? key;

  const netflixSteps = [1, 2, 3, 4].map((n) => t(`connections.guide.netflix.step${n}`));
  const koreaderSteps = [1, 2, 3, 4].map((n) => t(`connections.guide.koreader.step${n}`));

  return (
    <div className="max-w-2xl">
      <Link
        href="/connections"
        className="mb-4 inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="size-4" />
        {t('connections.guide.back')}
      </Link>

      <h1 className="mb-6 font-heading text-2xl font-semibold">{t('connections.guide.title')}</h1>

      <div className="flex flex-col gap-8">
        {/* Netflix */}
        <section id="netflix" className="scroll-mt-24">
          <div className="mb-3 flex items-center gap-2">
            <Puzzle className="size-5 text-primary" />
            <h2 className="font-heading text-lg font-semibold">{t('connections.netflix.name')}</h2>
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
            {netflixSteps.map((step, i) => (
              <li key={i}>{step}</li>
            ))}
          </ol>

          <p className="mt-3 text-xs text-muted-foreground">{t('connections.guide.netflix.reloadNote')}</p>
        </section>

        {/* KOReader */}
        <section id="koreader" className="scroll-mt-24">
          <div className="mb-3 flex items-center gap-2">
            <BookOpen className="size-5 text-primary" />
            <h2 className="font-heading text-lg font-semibold">{t('connections.koreader.name')}</h2>
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
            {koreaderSteps.map((step, i) => (
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
        </section>
      </div>
    </div>
  );
}
