import Link from 'next/link';
import { getServerT } from '@/lib/i18n-server';

/**
 * Site footer with links to the About, Credits and Privacy pages, the API docs (Swagger) and
 * the GitHub repository. Server Component, translated with `getServerT`.
 */
export default async function Footer() {
  const t = await getServerT();

  return (
    <footer className="mt-10 border-t border-border py-6">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-center gap-x-5 gap-y-2 px-4 text-xs text-muted-foreground/70 lg:px-6">
        <Link href="/about" className="transition-colors hover:text-muted-foreground">
          {t('footer.about')}
        </Link>
        <Link href="/credits" className="transition-colors hover:text-muted-foreground">
          {t('footer.credits')}
        </Link>
        <Link href="/privacy" className="transition-colors hover:text-muted-foreground">
          {t('footer.privacy')}
        </Link>
        <a
          href="/swagger"
          target="_blank"
          rel="noopener noreferrer"
          className="transition-colors hover:text-muted-foreground"
        >
          {t('footer.api')}
        </a>
        <a
          href="https://github.com/ElPandaP/TaleTrack"
          target="_blank"
          rel="noopener noreferrer"
          className="transition-colors hover:text-muted-foreground"
        >
          GitHub
        </a>
      </div>
    </footer>
  );
}
