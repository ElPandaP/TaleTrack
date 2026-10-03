'use client';

import Link from 'next/link';
import { useEffect, useState, type MouseEvent } from 'react';
import { MotionConfig, motion } from 'framer-motion';
import { Leaf, Menu, X } from 'lucide-react';
import ThemeToggle from '@/components/layout/ThemeToggle';
import LocaleToggle from '@/components/layout/LocaleToggle';
import { Cover } from '@/components/media/Cover';
import { Progress } from '@/components/ui/progress';
import { useI18n, useT } from '@/lib/i18n';
import type { LibraryType } from '@/lib/types';
import { cn } from '@/lib/utils';

/** Smooth-scrolls to an in-page anchor instead of jumping, and keeps the hash in the URL. */
function scrollToAnchor(e: MouseEvent<HTMLAnchorElement>) {
  const hash = e.currentTarget.hash;
  const target = hash ? document.querySelector(hash) : null;
  if (!target) return;
  e.preventDefault();
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  target.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
  history.replaceState(null, '', hash);
}

/* ── Navbar ───────────────────────────────────────────────── */
/** Landing header: logo, section links, toggles and sign-in/register; collapses into a menu on mobile. */
function Navbar() {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  // Close the mobile menu on Escape and when the viewport grows past the breakpoint.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    const mq = window.matchMedia('(min-width: 768px)');
    const onMq = () => mq.matches && setOpen(false);
    window.addEventListener('keydown', onKey);
    mq.addEventListener('change', onMq);
    return () => {
      window.removeEventListener('keydown', onKey);
      mq.removeEventListener('change', onMq);
    };
  }, [open]);

  const links = [
    { href: '#sources', label: t('landing.sources.heading') },
    { href: '#start', label: t('landing.start.heading') },
  ];

  return (
    <header
      className={cn(
        'sticky top-0 z-50 border-b transition-colors duration-200',
        scrolled || open ? 'border-border bg-background/95 backdrop-blur' : 'border-transparent bg-background',
      )}
    >
      {/* Three columns on desktop so the links sit on the page's centre line,
          whatever the widths of the logo and the right-hand controls. */}
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 md:grid md:grid-cols-[1fr_auto_1fr] lg:px-6">
        <Link href="/" className="flex items-center gap-2 justify-self-start rounded-md focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary">
          <Leaf className="h-5 w-5 text-primary" aria-hidden="true" />
          <span className="font-heading text-2xl font-semibold leading-none">TaleTrack</span>
        </Link>

        <nav aria-label={t('landing.nav.aria')} className="hidden items-center gap-8 md:flex">
          {links.map((l) => (
            <a key={l.href} href={l.href} onClick={scrollToAnchor} className="text-sm text-muted-foreground transition-colors hover:text-foreground">
              {l.label}
            </a>
          ))}
        </nav>

        <div className="hidden items-center gap-3 justify-self-end md:flex">
          <LocaleToggle />
          <ThemeToggle compact />
          <Link href="/login" className="px-2 text-sm font-medium text-foreground/80 transition-colors hover:text-foreground">
            {t('landing.nav.signIn')}
          </Link>
          <Link
            href="/register"
            className="rounded-lg bg-foreground px-4 py-2 text-sm font-medium text-background transition-opacity hover:opacity-85"
          >
            {t('landing.nav.register')}
          </Link>
        </div>

        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          aria-expanded={open}
          aria-controls="landing-mobile-menu"
          aria-label={open ? t('landing.nav.closeMenu') : t('landing.nav.openMenu')}
          className="-mr-2 flex h-10 w-10 cursor-pointer items-center justify-center rounded-lg text-foreground hover:bg-secondary md:hidden"
        >
          {open ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>
      </div>

      {open && (
        <div id="landing-mobile-menu" className="border-t border-border px-4 pb-5 pt-2 md:hidden">
          <nav aria-label={t('landing.nav.aria')} className="flex flex-col">
            {links.map((l) => (
              <a
                key={l.href}
                href={l.href}
                onClick={(e) => {
                  setOpen(false);
                  scrollToAnchor(e);
                }}
                className="border-b border-border/70 py-3 text-base text-foreground"
              >
                {l.label}
              </a>
            ))}
          </nav>
          <div className="mt-4 flex items-center gap-3">
            <LocaleToggle />
            <ThemeToggle compact />
          </div>
          <div className="mt-5 grid grid-cols-2 gap-3">
            <Link href="/login" className="rounded-lg border border-border py-2.5 text-center text-sm font-medium">
              {t('landing.nav.signIn')}
            </Link>
            <Link href="/register" className="rounded-lg bg-foreground py-2.5 text-center text-sm font-medium text-background">
              {t('landing.nav.register')}
            </Link>
          </div>
        </div>
      )}
    </header>
  );
}

/* ── Hero ─────────────────────────────────────────────────── */
/** A sample cover on the hero shelf; `wide` items only show from the `sm` breakpoint up. */
type Shelved = { title: string; type: LibraryType; poster: string; progress?: number; wide?: boolean };

/** Open Library cover URL for a cover id. */
const OL = (id: number) => `https://covers.openlibrary.org/b/id/${id}-M.jpg`;
/** TMDB poster URL for an image path. */
const TMDB = (path: string) => `https://image.tmdb.org/t/p/w342${path}`;

// Real covers from Open Library and TMDB, the same sources the app itself uses.
const SHELF: Shelved[] = [
  { title: 'The Name of the Wind', type: 'Book', poster: OL(8259445), progress: 32 },
  { title: 'Dune: Part Two', type: 'Movie', poster: TMDB('/6izwz7rsy95ARzTR3poZ8H6c5pp.jpg') },
  { title: 'Severance', type: 'Series', poster: TMDB('/pPHpeI2X1qEd1CS1SeyrdhZ4qnT.jpg'), progress: 60 },
  { title: 'Piranesi', type: 'Book', poster: OL(10226290) },
  { title: 'Past Lives', type: 'Movie', poster: TMDB('/k3waqVXSnvCZWfJYNtdamTgTtTA.jpg') },
  { title: 'The Bear', type: 'Series', poster: TMDB('/eKfVzzEazSIjJMrw9ADa2x8ksLz.jpg'), progress: 45 },
  { title: 'Circe', type: 'Book', poster: OL(8739376), wide: true },
  { title: 'Klara and the Sun', type: 'Book', poster: OL(10673548), wide: true },
];

/** Grid of sample covers that fade in one after another. Decorative, hidden from assistive tech. */
function Shelf() {
  return (
    <ul className="grid grid-cols-3 gap-x-3 gap-y-5 sm:grid-cols-4 sm:gap-x-4" aria-hidden="true">
      {SHELF.map((item, i) => (
        <motion.li
          key={item.title}
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 + i * 0.06, duration: 0.45, ease: [0.16, 1, 0.3, 1] }}
          className={cn(item.wide && 'hidden sm:block')}
        >
          <Cover title={item.title} type={item.type} posterUrl={item.poster} className="rounded-lg shadow-sm" />
          {item.progress !== undefined && <Progress value={item.progress} className="mt-2 h-1" />}
        </motion.li>
      ))}
    </ul>
  );
}

/** Headline, subtitle and calls to action next to the cover shelf. */
function Hero() {
  const t = useT();

  return (
    <section className="mx-auto grid max-w-6xl items-center gap-14 px-4 pb-16 pt-12 sm:pt-16 lg:grid-cols-[1.1fr_1fr] lg:gap-20 lg:px-6 lg:pb-24 lg:pt-20">
      <div>
        <h1 className="font-heading text-[2.9rem] font-semibold leading-[0.98] tracking-[-0.01em] text-balance sm:text-6xl lg:text-[4.6rem]">
          {t('landing.hero.heading')}
        </h1>
        <p className="mt-6 max-w-[34rem] text-base leading-relaxed text-muted-foreground sm:text-lg">
          {t('landing.hero.subtitle')}
        </p>
        <div className="mt-9 flex flex-wrap items-baseline gap-x-6 gap-y-4">
          <Link
            href="/register"
            className="rounded-lg bg-primary px-6 py-3 text-base font-medium text-primary-foreground transition-colors hover:bg-primary/90"
          >
            {t('landing.hero.cta')}
          </Link>
          <Link href="/login" className="text-sm font-medium text-foreground underline decoration-border decoration-2 underline-offset-4 transition-colors hover:decoration-foreground">
            {t('landing.hero.signIn')}
          </Link>
        </div>
      </div>

      <Shelf />
    </section>
  );
}

/* ── Sources ──────────────────────────────────────────────── */

/** A library row as the app shows it under "in progress". */
function LibraryRow({
  title, type, poster, meta, progress,
}: { title: string; type: LibraryType; poster?: string; meta: string; progress: number }) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-border bg-card p-3 shadow-sm">
      <Cover title={title} type={type} posterUrl={poster} className="w-11 shrink-0 rounded-md" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">{title}</p>
        <p className="truncate text-xs text-muted-foreground">{meta}</p>
        <div className="mt-2 flex items-center gap-2">
          <Progress value={progress} className="h-1" />
          <span className="shrink-0 text-[11px] text-muted-foreground">{progress}%</span>
        </div>
      </div>
    </div>
  );
}

/** A KOReader page: justified book text over the reader's own footer. */
function ReaderPage() {
  const { t, locale } = useI18n();
  return (
    <div className="rounded-[1.25rem] bg-[oklch(0.3_0.005_60)] p-2.5 shadow-sm">
      <div className="flex aspect-[3/4] flex-col bg-[oklch(0.95_0.003_95)] px-5 pb-2 pt-5 text-[oklch(0.2_0_0)]">
        <p className="flex-1 overflow-hidden text-justify font-serif text-[12.5px] leading-[1.55] hyphens-auto" lang={locale}>
          {t('landing.sources.reader.text')}
        </p>
        <div className="mt-3 flex items-center gap-2 text-[10px] tabular-nums" aria-hidden="true">
          <span>{t('landing.sources.reader.pages')}</span>
          <div className="relative h-1.5 flex-1 border border-current">
            <div className="h-full w-[20%] bg-current" />
          </div>
          <span>20%</span>
        </div>
      </div>
    </div>
  );
}

/** A browser window paused on an episode, with the extension in the toolbar.
 *  The extension shows nothing on the page itself; it works in the background. */
function BrowserWindow() {
  const t = useT();
  return (
    <div className="overflow-hidden rounded-xl border border-border bg-card shadow-sm">
      <div className="flex items-center gap-2 border-b border-border px-3 py-2">
        <span className="flex gap-1" aria-hidden="true">
          <span className="h-2 w-2 rounded-full bg-border" />
          <span className="h-2 w-2 rounded-full bg-border" />
          <span className="h-2 w-2 rounded-full bg-border" />
        </span>
        <span className="min-w-0 flex-1 truncate rounded-md bg-secondary px-2 py-0.5 text-[10px] text-muted-foreground">
          netflix.com/watch
        </span>
        <span
          className="relative flex h-5 w-5 shrink-0 items-center justify-center rounded-md border border-primary/25 bg-primary/15 text-primary"
          title="TaleTrack"
        >
          <Leaf className="h-3 w-3" aria-hidden="true" />
          <span className="absolute -right-0.5 -top-0.5 h-1.5 w-1.5 rounded-full bg-[oklch(0.6_0.16_150)] ring-2 ring-card" />
        </span>
      </div>
      <div className="relative aspect-video bg-[oklch(0.15_0.01_260)]">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src="https://image.tmdb.org/t/p/w500/mioOOhTaShLd1MPKsmfirbe9AwI.jpg"
          alt=""
          loading="lazy"
          className="absolute inset-0 h-full w-full object-cover opacity-80"
        />
        <div className="absolute inset-x-0 bottom-0 bg-linear-to-t from-black/85 to-transparent px-3 pb-2 pt-8 text-white">
          <p className="truncate text-[11px] font-semibold">
            Severance <span className="font-normal opacity-80">{t('landing.sources.player.episodeShort')}</span>
          </p>
          <div className="mt-1.5 h-0.5 rounded-full bg-white/30">
            <div className="h-full w-[58%] rounded-full bg-white" />
          </div>
          <div className="mt-1 flex justify-between text-[9px] tabular-nums opacity-75">
            <span>31:20</span>
            <span>54:00</span>
          </div>
        </div>
      </div>
    </div>
  );
}

/** "How it works" section: each source (KOReader, Netflix) next to the library row it produces. */
function Sources() {
  const t = useT();

  const rows = [
    {
      key: 'reader',
      source: <ReaderPage />,
      result: (
        <LibraryRow
          title={t('landing.sources.reader.book')}
          type="Book"
          meta={t('landing.sources.reader.author')}
          progress={20}
        />
      ),
    },
    {
      key: 'player',
      source: <BrowserWindow />,
      result: (
        <LibraryRow
          title="Severance"
          type="Series"
          poster={TMDB('/pPHpeI2X1qEd1CS1SeyrdhZ4qnT.jpg')}
          meta={t('landing.sources.player.episode')}
          progress={60}
        />
      ),
    },
  ] as const;

  return (
    <section id="sources" className="scroll-mt-16 border-t border-border bg-secondary/35">
      <div className="mx-auto max-w-6xl px-4 py-16 lg:px-6 lg:py-20">
        <h2 className="font-heading max-w-2xl text-4xl font-semibold leading-tight sm:text-5xl">
          {t('landing.sources.heading')}
        </h2>

        <div className="mt-10 divide-y divide-border border-t border-border">
          {rows.map(({ key, source, result }) => (
            <article key={key} className="grid gap-8 py-10 last:pb-0 md:grid-cols-[1fr_1.15fr] md:gap-16 md:py-12 md:last:pb-0">
              <div className="max-w-md">
                <h3 className="font-heading text-3xl font-semibold">{t(`landing.sources.${key}.title`)}</h3>
                <p className="mt-4 leading-relaxed text-muted-foreground">{t(`landing.sources.${key}.desc`)}</p>
              </div>

              <div className="grid items-center gap-5 sm:grid-cols-2">
                <div className="mx-auto w-full max-w-[16rem] sm:max-w-none">{source}</div>
                <div>
                  <p className="mb-2 text-xs text-muted-foreground">{t('landing.sources.inLibrary')}</p>
                  {result}
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

/* ── Getting started ──────────────────────────────────────── */
/** Three getting-started steps and the closing call to action. */
function Start() {
  const t = useT();
  const steps = ['step1', 'step2', 'step3'] as const;

  return (
    <section id="start" className="scroll-mt-16">
      <div className="mx-auto max-w-6xl px-4 py-16 lg:px-6 lg:py-20">
        <h2 className="font-heading text-4xl font-semibold sm:text-5xl">{t('landing.start.heading')}</h2>

        <ol className="mt-10 grid gap-10 md:grid-cols-3 md:gap-12">
          {steps.map((key, i) => (
            <li key={key} className="border-t-2 border-foreground pt-5">
              <span className="font-heading text-5xl font-semibold leading-none text-primary">{i + 1}</span>
              <h3 className="mt-4 font-sans text-lg font-semibold">{t(`landing.start.${key}.title`)}</h3>
              <p className="mt-2 leading-relaxed text-muted-foreground">{t(`landing.start.${key}.desc`)}</p>
            </li>
          ))}
        </ol>

        <div className="mt-16 flex flex-col items-start gap-6 border-t border-border pt-10 md:flex-row md:items-center md:justify-between">
          <p className="font-heading max-w-2xl text-3xl font-semibold leading-tight text-balance sm:text-4xl">
            {t('landing.closing.heading')}
          </p>
          <Link
            href="/register"
            className="shrink-0 rounded-lg bg-primary px-6 py-3 font-medium text-primary-foreground transition-colors hover:bg-primary/90"
          >
            {t('landing.closing.cta')}
          </Link>
        </div>
      </div>
    </section>
  );
}

/* ── Page ─────────────────────────────────────────────────── */
/** Public landing page shown at `/` to visitors without a session. */
export default function Landing() {
  return (
    <MotionConfig reducedMotion="user">
      <div className="bg-background font-sans text-foreground">
        <Navbar />
        <main>
          <Hero />
          <Sources />
          <Start />
        </main>
      </div>
    </MotionConfig>
  );
}
