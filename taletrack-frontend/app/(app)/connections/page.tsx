'use client';

import Link from 'next/link';
import { Puzzle, BookOpen, ArrowRight } from 'lucide-react';
import { useT } from '@/lib/i18n';
import ConnectionsCard from '@/components/connections/connections-card';

export default function ConnectionsPage() {
  const t = useT();

  const integrations = [
    {
      Icon: Puzzle,
      name: t('connections.netflix.name'),
      blurb: t('connections.netflix.blurb'),
      href: '/connections/guide/netflix',
    },
    {
      Icon: BookOpen,
      name: t('connections.koreader.name'),
      blurb: t('connections.koreader.blurb'),
      href: '/connections/guide/koreader',
    },
  ];

  return (
    <div className="max-w-2xl">
      <h1 className="mb-1 font-heading text-2xl font-semibold">{t('connections.title')}</h1>
      <p className="mb-6 text-sm text-muted-foreground">{t('connections.subtitle')}</p>

      <div className="flex flex-col gap-4">
        <ConnectionsCard />

        {integrations.map(({ Icon, name, blurb, href }) => (
          <section key={href} className="tt-card p-5">
            <div className="flex items-start gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
                <Icon className="size-5" />
              </span>
              <div className="min-w-0 flex-1">
                <h2 className="font-heading text-base font-semibold">{name}</h2>
                <p className="mt-1 text-sm text-foreground/90">{blurb}</p>
                <Link
                  href={href}
                  className="mt-3 inline-flex items-center gap-1.5 rounded-lg bg-primary px-3 py-2 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90"
                >
                  {t('connections.howTo')}
                  <ArrowRight className="size-4" />
                </Link>
              </div>
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
