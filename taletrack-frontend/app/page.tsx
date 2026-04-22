'use client';

import Link from 'next/link';
import { useState, useEffect, useRef } from 'react';
import { motion, useInView } from 'framer-motion';
import {
  Leaf, BookOpen, Film, Tv, BookMarked,
  ArrowRight, Zap, Shield, Star, Activity,
} from 'lucide-react';
import ThemeToggle from '@/components/layout/theme-toggle';

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
          <span className="font-heading font-semibold text-[17px] tracking-tight">TaleTrack</span>
        </div>

        <div className="hidden md:flex items-center gap-6">
          <a href="#features" className="text-sm text-muted-foreground hover:text-foreground transition-colors cursor-pointer">Features</a>
          <a href="#how-it-works" className="text-sm text-muted-foreground hover:text-foreground transition-colors cursor-pointer">How it works</a>
        </div>

        <div className="flex items-center gap-3">
          <ThemeToggle compact />
          <Link href="/login" className="hidden sm:block text-sm text-muted-foreground hover:text-foreground transition-colors">
            Sign in
          </Link>
          <Link href="/register"
            className="flex items-center gap-1.5 text-sm font-medium bg-primary text-primary-foreground px-4 py-2 rounded-xl hover:bg-primary/90 transition-colors">
            Get started <ArrowRight className="w-3.5 h-3.5" />
          </Link>
        </div>
      </div>
    </motion.nav>
  );
}

/* ── Dashboard mockup (floating card) ────────────────────── */
function DashboardMockup() {
  return (
    <motion.div
      animate={{ y: [0, -10, 0] }}
      transition={{ duration: 5, repeat: Infinity, ease: 'easeInOut' }}
      className="relative mt-14 max-w-lg mx-auto"
    >
      <div className="absolute -inset-6 bg-primary/8 rounded-[2.5rem] blur-3xl pointer-events-none" />
      <div className="relative bg-card border border-border rounded-2xl p-4 shadow-xl">
        {/* Mock header */}
        <div className="flex items-center justify-between mb-3.5 pb-3 border-b border-border">
          <div className="flex items-center gap-2">
            <Leaf className="w-3.5 h-3.5 text-primary" />
            <span className="text-xs font-semibold font-heading text-foreground">My Library</span>
          </div>
          <span className="text-[10px] text-muted-foreground">Good afternoon, reader</span>
        </div>

        {/* Stats row */}
        <div className="grid grid-cols-4 gap-2 mb-3.5">
          {[
            { label: 'Books', value: '24', Icon: BookOpen, color: 'text-[oklch(0.65_0.13_65)]', bg: 'bg-[oklch(0.65_0.13_65)]/10' },
            { label: 'Films', value: '87', Icon: Film,     color: 'text-[oklch(0.55_0.09_5)]', bg: 'bg-[oklch(0.55_0.09_5)]/10' },
            { label: 'Series', value: '12', Icon: Tv,      color: 'text-[oklch(0.52_0.09_152)]', bg: 'bg-[oklch(0.52_0.09_152)]/10' },
            { label: 'Comics', value: '6', Icon: BookMarked, color: 'text-[oklch(0.52_0.10_295)]', bg: 'bg-[oklch(0.52_0.10_295)]/10' },
          ].map(({ label, value, Icon, color, bg }) => (
            <div key={label} className={`${bg} border border-border/50 rounded-xl p-2.5 text-center`}>
              <Icon className={`w-3.5 h-3.5 ${color} mx-auto mb-1`} />
              <p className="text-sm font-bold text-foreground">{value}</p>
              <p className={`text-[10px] ${color}`}>{label}</p>
            </div>
          ))}
        </div>

        {/* Progress items */}
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
                  <span className="text-[10px] text-muted-foreground flex-shrink-0">{item.pct}%</span>
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
const heroWords = ['Every', 'story', 'finds', 'its'];
const heroItalic = 'place.';

function Hero() {
  return (
    <section className="relative min-h-screen flex flex-col items-center justify-center px-6 pt-28 pb-16 overflow-hidden">
      {/* Ambient blobs */}
      <div className="absolute top-1/4 left-1/4 w-[480px] h-[480px] bg-primary/6 rounded-full blur-[120px] pointer-events-none" />
      <div className="absolute bottom-1/3 right-1/4 w-[360px] h-[360px] bg-[oklch(0.65_0.13_65)]/6 rounded-full blur-[100px] pointer-events-none" />

      <div className="max-w-4xl mx-auto text-center relative z-10">
        {/* Eyebrow */}
        <motion.div
          initial={{ opacity: 0, scale: 0.92 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ duration: 0.5, ease: 'easeOut' }}
          className="inline-flex items-center gap-2 bg-primary/8 border border-primary/15 rounded-full px-4 py-1.5 text-sm text-primary mb-8"
        >
          <div className="w-1.5 h-1.5 bg-primary rounded-full animate-pulse" />
          Automatic tracking, zero effort
        </motion.div>

        {/* Heading */}
        <h1 className="font-heading font-semibold text-5xl md:text-7xl lg:text-[5.25rem] tracking-tight mb-6 leading-[1.08]">
          {heroWords.map((word, i) => (
            <motion.span
              key={word}
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
            {heroItalic}
          </motion.span>
        </h1>

        {/* Subtitle */}
        <motion.p
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.7, duration: 0.6, ease: 'easeOut' }}
          className="text-lg md:text-xl text-muted-foreground max-w-2xl mx-auto mb-10 leading-relaxed"
        >
          Books tracked page by page through KOReader. Films and series captured
          automatically via Netflix. One warm, beautiful library for everything you love.
        </motion.p>

        {/* CTAs */}
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.85, duration: 0.5 }}
          className="flex flex-col sm:flex-row items-center justify-center gap-4 mb-12"
        >
          <Link href="/register"
            className="flex items-center gap-2 px-8 py-4 bg-primary text-primary-foreground font-semibold rounded-2xl hover:bg-primary/90 transition-all duration-200 text-base shadow-lg shadow-primary/20">
            Start tracking free <ArrowRight className="w-5 h-5" />
          </Link>
          <a href="#how-it-works"
            className="flex items-center gap-2 px-8 py-4 bg-secondary/60 border border-border text-foreground font-medium rounded-2xl hover:bg-secondary transition-colors text-base cursor-pointer">
            See how it works
          </a>
        </motion.div>

        {/* Platform badges */}
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 1.0, duration: 0.5 }}
          className="flex items-center justify-center gap-5 text-sm text-muted-foreground/70"
        >
          <div className="flex items-center gap-1.5">
            <BookOpen className="w-4 h-4" />
            <span>KOReader plugin</span>
          </div>
          <div className="w-1 h-1 bg-border rounded-full" />
          <div className="flex items-center gap-1.5">
            <Film className="w-4 h-4" />
            <span>Netflix extension</span>
          </div>
          <div className="w-1 h-1 bg-border rounded-full" />
          <div className="flex items-center gap-1.5">
            <Zap className="w-4 h-4" />
            <span>Zero manual input</span>
          </div>
        </motion.div>
      </div>

      <DashboardMockup />
    </section>
  );
}

/* ── Features ─────────────────────────────────────────────── */
const features = [
  {
    Icon: BookOpen,
    title: 'KOReader Plugin',
    subtitle: 'Books & Comics',
    description: 'Every page turn captured. The lightweight plugin syncs your reading progress silently as you go — no interruptions, no manual input.',
    color: 'text-[oklch(0.65_0.13_65)]',
    bg: 'bg-[oklch(0.65_0.13_65)]/10',
    border: 'border-[oklch(0.65_0.13_65)]/15',
  },
  {
    Icon: Film,
    title: 'Netflix Extension',
    subtitle: 'Films & Series',
    description: 'Watch as you always do. The browser extension automatically records every film and series you stream, tracking progress in real time.',
    color: 'text-[oklch(0.55_0.09_5)]',
    bg: 'bg-[oklch(0.55_0.09_5)]/10',
    border: 'border-[oklch(0.55_0.09_5)]/15',
  },
  {
    Icon: Activity,
    title: 'Beautiful Library',
    subtitle: 'Your complete story',
    description: 'All your media in one warm home. Explore your reading and watching history, track progress, and celebrate every story you have lived.',
    color: 'text-primary',
    bg: 'bg-primary/10',
    border: 'border-primary/15',
  },
];

function Features() {
  const ref = useRef(null);
  const inView = useInView(ref, { once: true, margin: '-80px' });

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
            Everything you need
          </motion.p>
          <motion.h2
            variants={fadeUp}
            custom={1}
            initial="hidden"
            animate={inView ? 'visible' : 'hidden'}
            className="font-heading font-semibold text-4xl md:text-5xl tracking-tight mb-4"
          >
            Built around your{' '}
            <span className="text-muted-foreground italic">existing habits</span>
          </motion.h2>
          <motion.p
            variants={fadeUp}
            custom={2}
            initial="hidden"
            animate={inView ? 'visible' : 'hidden'}
            className="text-muted-foreground text-lg max-w-xl mx-auto"
          >
            No new apps to open. No manual logging. TaleTrack works entirely in the background.
          </motion.p>
        </div>

        <motion.div
          variants={stagger}
          initial="hidden"
          animate={inView ? 'visible' : 'hidden'}
          className="grid grid-cols-1 md:grid-cols-3 gap-5"
        >
          {features.map(({ Icon, title, subtitle, description, color, bg, border }) => (
            <motion.div
              key={title}
              variants={fadeUp}
              className={`tt-card p-6 ${border} hover:shadow-md transition-all duration-300 group`}
            >
              <div className={`w-11 h-11 ${bg} ${border} border rounded-2xl flex items-center justify-center mb-5 group-hover:scale-105 transition-transform duration-300`}>
                <Icon className={`w-5 h-5 ${color}`} />
              </div>
              <p className={`text-[10px] font-semibold ${color} uppercase tracking-widest mb-1`}>
                {subtitle}
              </p>
              <h3 className="font-heading text-xl font-semibold mb-2.5">{title}</h3>
              <p className="text-muted-foreground text-sm leading-relaxed">{description}</p>
            </motion.div>
          ))}
        </motion.div>
      </div>
    </section>
  );
}

/* ── How it works ─────────────────────────────────────────── */
const steps = [
  {
    step: '01',
    title: 'Install once',
    description: 'Add the KOReader plugin to your e-reader and the browser extension for Netflix. A two-minute setup, done forever.',
  },
  {
    step: '02',
    title: 'Read & watch',
    description: 'Enjoy your books, films, and series exactly as you normally do. TaleTrack works silently in the background.',
  },
  {
    step: '03',
    title: 'Explore your library',
    description: 'Open TaleTrack and find your complete history already organised and beautifully displayed.',
  },
];

function HowItWorks() {
  const ref = useRef(null);
  const inView = useInView(ref, { once: true, margin: '-80px' });

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
            Three steps. Then forget.
          </motion.h2>
          <motion.p
            variants={fadeUp}
            custom={1}
            initial="hidden"
            animate={inView ? 'visible' : 'hidden'}
            className="text-muted-foreground text-lg max-w-xl mx-auto"
          >
            TaleTrack is designed to disappear. After setup, your library grows on its own.
          </motion.p>
        </div>

        <motion.div
          variants={stagger}
          initial="hidden"
          animate={inView ? 'visible' : 'hidden'}
          className="grid grid-cols-1 md:grid-cols-3 gap-8 relative"
        >
          <div className="hidden md:block absolute top-[52px] left-[calc(16.66%+24px)] right-[calc(16.66%+24px)] h-px bg-border" />

          {steps.map(({ step, title, description }) => (
            <motion.div key={step} variants={fadeUp} className="flex flex-col items-center text-center">
              <div className="w-16 h-16 bg-primary/10 border border-primary/15 rounded-2xl flex items-center justify-center mb-5">
                <span className="font-heading text-xl font-bold text-primary">{step}</span>
              </div>
              <h3 className="font-heading text-xl font-semibold mb-2.5">{title}</h3>
              <p className="text-muted-foreground text-sm leading-relaxed max-w-xs">{description}</p>
            </motion.div>
          ))}
        </motion.div>
      </div>
    </section>
  );
}

/* ── Properties strip ─────────────────────────────────────── */
function Properties() {
  const ref = useRef(null);
  const inView = useInView(ref, { once: true, margin: '-60px' });

  const props = [
    { Icon: Zap,      label: 'Automatic', desc: 'Zero manual input ever' },
    { Icon: Shield,   label: 'Private',   desc: 'Your data, your control' },
    { Icon: Star,     label: 'Beautiful', desc: 'Designed for readers' },
    { Icon: Activity, label: 'Real-time', desc: 'Always up to date' },
  ];

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
          {props.map(({ Icon, label, desc }) => (
            <motion.div key={label} variants={fadeUp} className="text-center">
              <div className="w-10 h-10 bg-primary/10 border border-primary/15 rounded-xl flex items-center justify-center mx-auto mb-3">
                <Icon className="w-4.5 h-4.5 text-primary" />
              </div>
              <p className="font-heading font-semibold text-base mb-1">{label}</p>
              <p className="text-xs text-muted-foreground">{desc}</p>
            </motion.div>
          ))}
        </motion.div>
      </div>
    </section>
  );
}

/* ── CTA ──────────────────────────────────────────────────── */
function CTA() {
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
            Ready to begin your story?
          </motion.h2>
          <motion.p variants={fadeUp} custom={1} className="text-muted-foreground text-lg mb-10">
            Create your free account today. No credit card required.
          </motion.p>
          <motion.div variants={fadeUp} custom={2} className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <Link href="/register"
              className="flex items-center gap-2 px-8 py-4 bg-primary text-primary-foreground font-semibold rounded-2xl hover:bg-primary/90 transition-all text-base shadow-lg shadow-primary/20">
              Create free account <ArrowRight className="w-5 h-5" />
            </Link>
            <Link href="/login" className="text-muted-foreground hover:text-foreground text-sm transition-colors">
              Already have an account? Sign in →
            </Link>
          </motion.div>
        </motion.div>
      </div>
    </section>
  );
}

/* ── Footer ───────────────────────────────────────────────── */
function Footer() {
  return (
    <footer className="border-t border-border py-10 px-6">
      <div className="max-w-5xl mx-auto flex flex-col md:flex-row items-center justify-between gap-5">
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 bg-primary/15 border border-primary/25 rounded-lg flex items-center justify-center">
            <Leaf className="w-3.5 h-3.5 text-primary" />
          </div>
          <span className="font-heading font-semibold text-sm">TaleTrack</span>
          <span className="text-muted-foreground/50 text-sm">— Every story finds its place</span>
        </div>
        <div className="flex items-center gap-6 text-sm text-muted-foreground">
          <Link href="/login"    className="hover:text-foreground transition-colors">Sign in</Link>
          <Link href="/register" className="hover:text-foreground transition-colors">Register</Link>
          <span className="text-muted-foreground/40">© 2025 TaleTrack</span>
        </div>
      </div>
    </footer>
  );
}

/* ── Page ─────────────────────────────────────────────────── */
export default function LandingPage() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <Navbar />
      <Hero />
      <div className="relative">
        <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-border to-transparent" />
        <Features />
      </div>
      <div className="relative">
        <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-border to-transparent" />
        <HowItWorks />
      </div>
      <Properties />
      <CTA />
      <Footer />
    </div>
  );
}
