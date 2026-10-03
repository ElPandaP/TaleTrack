import { getServerT } from '@/lib/i18n-server';

const AUTHOR = 'Pelayo García Varela';
const AUTHOR_URL = 'https://github.com/ElPandaP';

/** `/about`: what TaleTrack is and who made it. Public Server Component. */
export default async function AboutPage() {
  const t = await getServerT();
  const [beforeAuthor, afterAuthor] = t('about.tfg').split('{author}');

  return (
    <div className="max-w-2xl">
      <h1 className="mb-1 font-heading text-2xl font-semibold">{t('about.title')}</h1>
      <p className="mb-6 text-sm text-muted-foreground">{t('about.subtitle')}</p>

      <div className="divide-y divide-border leading-relaxed text-foreground/90">
        <p className="py-5 first:pt-0">{t('about.what')}</p>
        <p className="py-5 first:pt-0">
          {beforeAuthor}
          <a href={AUTHOR_URL} target="_blank" rel="noopener noreferrer" className="text-primary hover:underline">
            {AUTHOR}
          </a>
          {afterAuthor}
        </p>
      </div>
    </div>
  );
}
