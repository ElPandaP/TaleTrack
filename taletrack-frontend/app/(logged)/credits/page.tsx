import { getServerT } from '@/lib/i18n-server';

/**
 * `/credits`: attribution for the data sources (TMDB and Open Library). Public Server
 * Component. The TMDB attribution notice is kept verbatim in English in both locales.
 */
export default async function CreditsPage() {
  const t = await getServerT();

  return (
    <div className="max-w-2xl">
      <h1 className="mb-1 font-heading text-2xl font-semibold">{t('credits.title')}</h1>
      <p className="mb-6 text-sm text-muted-foreground">{t('credits.subtitle')}</p>

      <div className="divide-y divide-border">
        <section className="py-5 first:pt-0">
          <h2 className="mb-2 font-heading text-lg font-semibold">{t('credits.tmdb.heading')}</h2>
          <p className="leading-relaxed text-foreground/90">{t('credits.tmdb.body')}</p>
          <p className="mt-3 text-xs text-muted-foreground">
            This application uses TMDB and the TMDB APIs but is not endorsed, certified, or
            otherwise approved by TMDB.
          </p>
          <a
            href="https://www.themoviedb.org/"
            target="_blank"
            rel="noopener noreferrer"
            className="mt-2 inline-block text-sm text-primary hover:underline"
          >
            themoviedb.org
          </a>
        </section>

        <section className="py-5 first:pt-0">
          <h2 className="mb-2 font-heading text-lg font-semibold">{t('credits.openlibrary.heading')}</h2>
          <p className="leading-relaxed text-foreground/90">{t('credits.openlibrary.body')}</p>
          <a
            href="https://openlibrary.org/"
            target="_blank"
            rel="noopener noreferrer"
            className="mt-2 inline-block text-sm text-primary hover:underline"
          >
            openlibrary.org
          </a>
        </section>
      </div>
    </div>
  );
}
