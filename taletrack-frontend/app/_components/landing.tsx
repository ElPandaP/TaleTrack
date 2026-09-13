'use client';

import Link from 'next/link';
import { useState, useEffect, useRef } from 'react';
import { motion, useInView } from 'framer-motion';
import {
  Leaf, BookOpen, Film, Tv,
  ArrowRight, Zap, Shield, Star, Activity,
} from 'lucide-react';
import ThemeToggle from '@/components/layout/theme-toggle';
import LocaleToggle from '@/components/layout/locale-toggle';
import { useT } from '@/lib/i18n';

/* ── Shared animation variants ────────────────────────────── */
const fadeUp = {
  hidden: { opacity: 0, y: 24 },
  visible: (i: number = 0) => ({
    opacity: 1, y: 0,
    transition: { delay: i * 0.1, duration: 0.6, ease: [0.16, 1, 0.3, 1] as [number, number, number, number] },
  }),
};

const stagger = {
  hidden: {},
  visible: { transition: { staggerChildren: 0.12 } },
};

/* ── Navbar ───────────────────────────────────────────────── */
function Navbar() {
  const t = useT();
  const [scrolled, setScrolled] = useState(false);
  useEffect(() => {
    const fn = () => setScrolled(window.scrollY > 24);
    window.addEventListener('scroll', fn, { passive: true });
    return () => window.removeEventListener('scroll', fn);
  }, []);

  return (
    <motion.nav
      initial={{ y: -20, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ duration: 0.5, ease: 'easeOut' }}
      className="fixed top-0 left-0 right-0 z-50 px-4 pt-4"
    >
      <div className={`max-w-5xl mx-auto flex items-center justify-between px-5 py-3 rounded-2xl border transition-all duration-300 ${
        scrolled
          ? 'bg-card/90 backdrop-blur-md border-border shadow-sm'
          : 'bg-transparent border-transparent'
      }`}>
        <div className="flex items-center gap-2.5">
          <div className="w-8 h-8 bg-primary/15 border border-primary/25 rounded-xl flex items-center justify-center">
            <Leaf className="w-4 h-4 text-primary" />
          </div>
          <Link href="/" className="font-heading font-semibold text-[17px] tracking-tight cursor-pointer">TaleTrack</Link>
        </div>

        <div className="hidden md:flex items-center gap-6">
          <a href="#features" className="text-sm text-muted-foreground hover:text-foreground transition-colors cursor-pointer">{t('landing.nav.features')}</a>
          <a href="#how-it-works" className="text-sm text-muted-foreground hover:text-foreground transition-colors cursor-pointer">{t('landing.nav.howItWorks')}</a>
        </div>

        <div className="flex items-center gap-3">
          <LocaleToggle />
          <ThemeToggle compact />
          <Link href="/login" className="hidden sm:block text-sm text-muted-foreground hover:text-foreground transition-colors">
            {t('landing.nav.signIn')}
          </Link>
          <Link href="/register"
            className="flex items-center gap-1.5 text-sm font-medium bg-primary text-primary-foreground px-4 py-2 rounded-xl hover:bg-primary/90 transition-colors">
            {t('landing.nav.getStarted')} <ArrowRight className="w-3.5 h-3.5" />
          </Link>
        </div>
      </div>
    </motion.nav>
  );
}

/* ── Dashboard mockup (floating card) ────────────────────── */
function DashboardMockup() {
  const t = useT();
  const stats = [
    { label: t('landing.mock.books'), value: '24', Icon: BookOpen, color: 'text-[oklch(0.65_0.13_65)]', bg: 'bg-[oklch(0.65_0.13_65)]/10' },
    { label: t('landing.mock.films'), value: '87', Icon: Film, color: 'text-[oklch(0.55_0.09_5)]', bg: 'bg-[oklch(0.55_0.09_5)]/10' },
    { label: t('landing.mock.series'), value: '12', Icon: Tv, color: 'text-[oklch(0.52_0.09_152)]', bg: 'bg-[oklch(0.52_0.09_152)]/10' },
  ];

  return (
    <motion.div
      animate={{ y: [0, -10, 0] }}
      transition={{ duration: 5, repeat: Infinity, ease: 'easeInOut' }}
      className="relative mt-14 max-w-lg mx-auto"
    >
      <div className="absolute -inset-6 bg-primary/8 rounded-[2.5rem] blur-3xl pointer-events-none" />
      <div className="relative bg-card border border-border rounded-2xl p-4 shadow-xl">
        <div className="flex items-center justify-between mb-3.5 pb-3 border-b border-border">
          <div className="flex items-center gap-2">
            <Leaf className="w-3.5 h-3.5 text-primary" />
            <span className="text-xs font-semibold font-heading text-foreground">{t('landing.mock.title')}</span>
          </div>
          <span className="text-[10px] text-muted-foreground">{t('landing.mock.greeting')}</span>
        </div>

        <div className="grid grid-cols-3 gap-2 mb-3.5">
          {stats.map(({ label, value, Icon, color, bg }) => (
            <div key={label} className={`${bg} border border-border/50 rounded-xl p-2.5 text-center`}>
              <Icon className={`w-3.5 h-3.5 ${color} mx-auto mb-1`} />
              <p className="text-sm font-bold text-foreground">{value}</p>
              <p className={`text-[10px] ${color}`}>{label}</p>
            </div>
          ))}
        </div>

        <div className="space-y-2">
          {[
            { title: 'Dune: Part Two', pct: 72, barColor: 'bg-[oklch(0.55_0.09_5)]' },
            { title: 'The Name of the Wind', pct: 45, barColor: 'bg-[oklch(0.65_0.13_65)]' },
            { title: 'Severance', pct: 89, barColor: 'bg-[oklch(0.52_0.09_152)]' },
          ].map((item, i) => (
            <motion.div
              key={item.title}
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: 0.9 + i * 0.15, duration: 0.5 }}
              className="flex items-center gap-3 px-2.5 py-2 bg-secondary/50 rounded-xl"
            >
              <div className="flex-1 min-w-0">
                <p className="text-xs font-medium text-foreground truncate">{item.title}</p>
                <div className="flex items-center gap-2 mt-1">
                  <div className="flex-1 h-1.5 bg-border rounded-full overflow-hidden">
                    <motion.div
                      className={`h-full ${item.barColor} rounded-full`}
                      initial={{ width: 0 }}
                      animate={{ width: `${item.pct}%` }}
                      transition={{ delay: 1.1 + i * 0.2, duration: 0.9, ease: 'easeOut' }}
                    />
                  </div>
                  <span className="text-[10px] text-muted-foreground shrink-0">{item.pct}%</span>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </motion.div>
  );
}

/* ── Hero ─────────────────────────────────────────────────── */
function Hero() {
  const t = useT();
  const heroWords = t('landing.hero.headingWords').split(' ');

  return (
    <section className="relative min-h-screen flex flex-col items-center justify-center px-6 pt-28 pb-16 overflow-hidden">
      <div className="absolute top-1/4 left-1/4 w-120 h-120 bg-primary/6 rounded-full blur-[120px] pointer-events-none" />
      <div className="absolute bottom-1/3 right-1/4 w-90 h-90 bg-[oklch(0.65_0.13_65)]/6 rounded-full blur-[100px] pointer-events-none" />

      <div className="max-w-4xl mx-auto text-center relative z-10">
        <motion.div
          initial={{ opacity: 0, scale: 0.92 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ duration: 0.5, ease: 'easeOut' }}
          className="inline-flex items-center gap-2 bg-primary/8 border border-primary/15 rounded-full px-4 py-1.5 text-sm text-primary mb-8"
        >
          <div className="w-1.5 h-1.5 bg-primary rounded-full animate-pulse" />
          {t('landing.hero.eyebrow')}
        </motion.div>

        <h1 className="font-heading font-semibold text-5xl md:text-7xl lg:text-[5.25rem] tracking-tight mb-6 leading-[1.08]">
          {heroWords.map((word, i) => (
            <motion.span
              key={`${word}-${i}`}
              custom={i}
              variants={fadeUp}
              initial="hidden"
              animate="visible"
              className="inline-block mr-[0.22em]"
            >
              {word}
            </motion.span>
          ))}
          <motion.span
            custom={heroWords.length}
            variants={fadeUp}
            initial="hidden"
            animate="visible"
            className="inline-block text-primary italic"
          >
            {t('landing.hero.headingItalic')}
          </motion.span>
        </h1>

        <motion.p
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.7, duration: 0.6, ease: 'easeOut' }}
          className="text-lg md:text-xl text-muted-foreground max-w-2xl mx-auto mb-10 leading-relaxed"
        >
          {t('landing.hero.subtitle')}
        </motion.p>

        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.85, duration: 0.5 }}
          className="flex flex-col sm:flex-row items-center justify-center gap-4 mb-12"
        >
          <Link href="/register"
            className="flex items-center gap-2 px-8 py-4 bg-primary text-primary-foreground font-semibold rounded-2xl hover:bg-primary/90 transition-all duration-200 text-base shadow-lg shadow-primary/20">
            {t('landing.hero.ctaPrimary')} <ArrowRight className="w-5 h-5" />
          </Link>
          <a href="#how-it-works"
            className="flex items-center gap-2 px-8 py-4 bg-secondary/60 border border-border text-foreground font-medium rounded-2xl hover:bg-secondary transition-colors text-base cursor-pointer">
            {t('landing.hero.ctaSecondary')}
          </a>
        </motion.div>

        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 1.0, duration: 0.5 }}
          className="flex items-center justify-center gap-5 text-sm text-muted-foreground/70"
        >
          <div className="flex items-center gap-1.5">
            <BookOpen className="w-4 h-4" />
            <span>{t('landing.hero.badge.koreader')}</span>
          </div>
          <div className="w-1 h-1 bg-border rounded-full" />
          <div className="flex items-center gap-1.5">
            <Film className="w-4 h-4" />
            <span>{t('landing.hero.badge.netflix')}</span>
          </div>
          <div className="w-1 h-1 bg-border rounded-full" />
          <div className="flex items-center gap-1.5">
            <Zap className="w-4 h-4" />
            <span>{t('landing.hero.badge.zero')}</span>
          </div>
        </motion.div>
      </div>

      <DashboardMockup />
    </section>
  );
}

/* ── Features ─────────────────────────────────────────────── */
function Features() {
  const t = useT();
  const ref = useRef(null);
  const inView = useInView(ref, { once: true, margin: '-80px' });

  const features = [
    { Icon: BookOpen, key: 'koreader', color: 'text-[oklch(0.65_0.13_65)]', bg: 'bg-[oklch(0.65_0.13_65)]/10', border: 'border-[oklch(0.65_0.13_65)]/15' },
    { Icon: Film, key: 'netflix', color: 'text-[oklch(0.55_0.09_5)]', bg: 'bg-[oklch(0.55_0.09_5)]/10', border: 'border-[oklch(0.55_0.09_5)]/15' },
    { Icon: Activity, key: 'library', color: 'text-primary', bg: 'bg-primary/10', border: 'border-primary/15' },
  ] as const;

  return (
    <section id="features" className="py-24 px-6">
      <div className="max-w-5xl mx-auto">
        <div className="text-center mb-14">
          <motion.p
            ref={ref}
            variants={fadeUp}
            initial="hidden"
            animate={inView ? 'visible' : 'hidden'}
            className="text-[11px] font-semibold text-muted-foreground/60 uppercase tracking-widest mb-3"
          >
            {t('landing.features.eyebrow')}
          </motion.p>
          <motion.h2
            variants={fadeUp}
            custom={1}
            initial="hidden"
            animate={inView ? 'visible' : 'hidden'}
            className="font-heading font-semibold text-4xl md:text-5xl tracking-tight mb-4"
          >
            {t('landing.features.headingA')}{' '}
            <span className="text-muted-foreground italic">{t('landing.features.headingB')}</span>
          </motion.h2>
          <motion.p
            variants={fadeUp}
            custom={2}
            initial="hidden"
            animate={inView ? 'visible' : 'hidden'}
            className="text-muted-foreground text-lg max-w-xl mx-auto"
          >
            {t('landing.features.subtitle')}
          </motion.p>
        </div>

        <motion.div
          variants={stagger}
          initial="hidden"
          animate={inView ? 'visible' : 'hidden'}
          className="grid grid-cols-1 md:grid-cols-3 gap-5"
        >
          {features.map(({ Icon, key, color, bg, border }) => (
            <motion.div
              key={key}
              variants={fadeUp}
              className={`tt-card p-6 ${border} hover:shadow-md transition-all duration-300 group`}
            >
              <div className={`w-11 h-11 ${bg} ${border} border rounded-2xl flex items-center justify-center mb-5 group-hover:scale-105 transition-transform duration-300`}>
                <Icon className={`w-5 h-5 ${color}`} />
              </div>
              <p className={`text-[10px] font-semibold ${color} uppercase tracking-widest mb-1`}>
                {t(`landing.features.${key}.subtitle`)}
              </p>
              <h3 className="font-heading text-xl font-semibold mb-2.5">{t(`landing.features.${key}.title`)}</h3>
              <p className="text-muted-foreground text-sm leading-relaxed">{t(`landing.features.${key}.desc`)}</p>
            </motion.div>
          ))}
        </motion.div>
      </div>
    </section>
  );
}

/* ── How it works ─────────────────────────────────────────── */
function HowItWorks() {
  const t = useT();
  const ref = useRef(null);
  const inView = useInView(ref, { once: true, margin: '-80px' });
  const steps = ['step1', 'step2', 'step3'] as const;

  return (
    <section id="how-it-works" className="py-24 px-6">
      <div className="max-w-5xl mx-auto">
        <div className="text-center mb-14">
          <motion.h2
            ref={ref}
            variants={fadeUp}
            initial="hidden"
            animate={inView ? 'visible' : 'hidden'}
            className="font-heading font-semibold text-4xl md:text-5xl tracking-tight mb-4"
          >
            {t('landing.how.heading')}
          </motion.h2>
          <motion.p
            variants={fadeUp}
            custom={1}
            initial="hidden"
            animate={inView ? 'visible' : 'hidden'}
            className="text-muted-foreground text-lg max-w-xl mx-auto"
          >
            {t('landing.how.subtitle')}
          </motion.p>
        </div>

        <motion.div
          variants={stagger}
          initial="hidden"
          animate={inView ? 'visible' : 'hidden'}
          className="grid grid-cols-1 md:grid-cols-3 gap-8 relative"
        >
          <div className="hidden md:block absolute top-13 left-[calc(16.66%+24px)] right-[calc(16.66%+24px)] h-px bg-border pointer-events-none" />

          {steps.map((key, i) => (
            <motion.div key={key} variants={fadeUp} className="flex flex-col items-center text-center relative z-10">
              <div className="p-1 bg-background rounded-2xl mb-5">
                <div className="w-16 h-16 bg-primary/10 border border-primary/15 rounded-xl flex items-center justify-center">
                  <span className="font-heading text-xl font-bold text-primary">{`0${i + 1}`}</span>
                </div>
              </div>
              <h3 className="font-heading text-xl font-semibold mb-2.5">{t(`landing.how.${key}.title`)}</h3>
              <p className="text-muted-foreground text-sm leading-relaxed max-w-xs">{t(`landing.how.${key}.desc`)}</p>
            </motion.div>
          ))}
        </motion.div>
      </div>
    </section>
  );
}

/* ── Properties strip ─────────────────────────────────────── */
function Properties() {
  const t = useT();
  const ref = useRef(null);
  const inView = useInView(ref, { once: true, margin: '-60px' });

  const props = [
    { Icon: Zap, key: 'automatic' },
    { Icon: Shield, key: 'private' },
    { Icon: Star, key: 'beautiful' },
    { Icon: Activity, key: 'realtime' },
  ] as const;

  return (
    <section className="py-16 px-6">
      <div className="max-w-4xl mx-auto">
        <motion.div
          ref={ref}
          variants={stagger}
          initial="hidden"
          animate={inView ? 'visible' : 'hidden'}
          className="tt-card p-8 md:p-12 grid grid-cols-2 md:grid-cols-4 gap-8"
        >
          {props.map(({ Icon, key }) => (
            <motion.div key={key} variants={fadeUp} className="text-center">
              <div className="w-10 h-10 bg-primary/10 border border-primary/15 rounded-xl flex items-center justify-center mx-auto mb-3">
                <Icon className="w-4.5 h-4.5 text-primary" />
              </div>
              <p className="font-heading font-semibold text-base mb-1">{t(`landing.props.${key}`)}</p>
              <p className="text-xs text-muted-foreground">{t(`landing.props.${key}Desc`)}</p>
            </motion.div>
          ))}
        </motion.div>
      </div>
    </section>
  );
}

/* ── CTA ──────────────────────────────────────────────────── */
function CTA() {
  const t = useT();
  const ref = useRef(null);
  const inView = useInView(ref, { once: true, margin: '-60px' });

  return (
    <section className="py-24 px-6">
      <div className="max-w-2xl mx-auto text-center relative">
        <div className="absolute inset-0 bg-primary/5 rounded-3xl blur-3xl pointer-events-none" />
        <motion.div
          ref={ref}
          variants={stagger}
          initial="hidden"
          animate={inView ? 'visible' : 'hidden'}
          className="relative"
        >
          <motion.h2 variants={fadeUp} className="font-heading font-semibold text-4xl md:text-5xl tracking-tight mb-4">
            {t('landing.cta.heading')}
          </motion.h2>
          <motion.p variants={fadeUp} custom={1} className="text-muted-foreground text-lg mb-10">
            {t('landing.cta.subtitle')}
          </motion.p>
          <motion.div variants={fadeUp} custom={2} className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <Link href="/register"
              className="flex items-center gap-2 px-8 py-4 bg-primary text-primary-foreground font-semibold rounded-2xl hover:bg-primary/90 transition-all text-base shadow-lg shadow-primary/20">
              {t('landing.cta.button')} <ArrowRight className="w-5 h-5" />
            </Link>
            <Link href="/login" className="text-muted-foreground hover:text-foreground text-sm transition-colors">
              {t('landing.cta.signIn')}
            </Link>
          </motion.div>
        </motion.div>
      </div>
    </section>
  );
}

/* ── Footer ───────────────────────────────────────────────── */
function Footer() {
  const t = useT();
  return (
    <footer className="border-t border-border py-10 px-6">
      <div className="max-w-5xl mx-auto flex flex-col md:flex-row items-center justify-between gap-5">
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 bg-primary/15 border border-primary/25 rounded-lg flex items-center justify-center">
            <Leaf className="w-3.5 h-3.5 text-primary" />
          </div>
          <span className="font-heading font-semibold text-sm">TaleTrack</span>
          <span className="text-muted-foreground/50 text-sm">{t('landing.footer.tagline')}</span>
        </div>
        <div className="flex items-center gap-6 text-sm text-muted-foreground">
          <Link href="/login" className="hover:text-foreground transition-colors">{t('landing.footer.signIn')}</Link>
          <Link href="/register" className="hover:text-foreground transition-colors">{t('landing.footer.register')}</Link>
          <span className="text-muted-foreground/40">{t('landing.footer.copy')}</span>
        </div>
      </div>
    </footer>
  );
}

/* ── Page ─────────────────────────────────────────────────── */
export default function Landing() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <Navbar />
      <Hero />
      <div className="relative">
        <div className="absolute inset-x-0 top-0 h-px bg-linear-to-r from-transparent via-border to-transparent" />
        <Features />
      </div>
      <div className="relative">
        <div className="absolute inset-x-0 top-0 h-px bg-linear-to-r from-transparent via-border to-transparent" />
        <HowItWorks />
      </div>
      <Properties />
      <CTA />
      <Footer />
    </div>
  );
}
