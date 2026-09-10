'use client';

import Link from 'next/link';
import { useT } from '@/lib/i18n';

export default function Footer() {
  const t = useT();

  return (
    <footer className="mt-10 border-t border-border py-6">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-center gap-x-5 gap-y-2 px-4 text-xs text-muted-foreground/70 lg:px-6">
        <Link href="/about" className="transition-colors hover:text-muted-foreground">
          {t('footer.credits')}
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
