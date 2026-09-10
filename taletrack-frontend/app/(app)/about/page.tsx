import { getServerDict } from '@/lib/i18n-server';

export default async function AboutPage() {
  const dict = await getServerDict();
  const t = (key: string) => dict[key] ?? key;

  return (
    <div className="max-w-2xl">
      <h1 className="mb-1 font-heading text-2xl font-semibold">{t('about.title')}</h1>
      <p className="mb-6 text-sm text-muted-foreground">{t('about.subtitle')}</p>

      <div className="flex flex-col gap-4">
        <section className="tt-card p-4">
          <h2 className="mb-2 font-heading text-base font-semibold">{t('about.tmdb.heading')}</h2>
          <p className="text-sm text-foreground/90">{t('about.tmdb.body')}</p>
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

        <section className="tt-card p-4">
          <h2 className="mb-2 font-heading text-base font-semibold">{t('about.openlibrary.heading')}</h2>
          <p className="text-sm text-foreground/90">{t('about.openlibrary.body')}</p>
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
