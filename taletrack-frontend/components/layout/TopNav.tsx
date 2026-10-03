'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import { motion, useReducedMotion } from 'framer-motion';
import { Leaf, User, LogOut, Users } from 'lucide-react';
import { useAuth } from '@/lib/auth-context';
import { useT } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import { UserAvatar } from '@/components/media/UserAvatar';
import { Button } from '@/components/ui/button';
import ThemeToggle from './ThemeToggle';
import LocaleToggle from './LocaleToggle';

/** Main navigation links; `exact` marks the one that is only active on its own path. */
const navItems = [
  { href: '/', key: 'nav.home', exact: true },
  { href: '/library', key: 'nav.library' },
  { href: '/reviews', key: 'nav.reviews' },
  { href: '/activity', key: 'nav.activity' },
  { href: '/connections', key: 'nav.connections' },
];

/** Position and size of the active-link highlight, relative to the nav. */
type PillRect = { left: number; top: number; width: number; height: number };

/**
 * The row of main navigation links, with a highlight that slides to the active one.
 *
 * @remarks
 * The highlight measures the active link and animates its position and width directly: a
 * shared framer-motion `layoutId` does not survive Next.js segment swaps, so it would jump
 * instead of sliding.
 */
function NavItems({ className, compact = false }: { className?: string; compact?: boolean }) {
  const pathname = usePathname();
  const reduceMotion = useReducedMotion();
  const t = useT();
  const navRef = useRef<HTMLElement>(null);
  const [pill, setPill] = useState<PillRect | null>(null);
  const pad = compact ? 'px-3 py-1' : 'px-3 py-1.5';

  useEffect(() => {
    const nav = navRef.current;
    if (!nav) return;

    const measure = () => {
      const el = nav.querySelector<HTMLElement>('[data-nav-active="true"]');
      const next: PillRect | null = el
        ? { left: el.offsetLeft, top: el.offsetTop, width: el.offsetWidth, height: el.offsetHeight }
        : null;
      setPill((prev) =>
        prev && next &&
        prev.left === next.left && prev.top === next.top &&
        prev.width === next.width && prev.height === next.height
          ? prev
          : next,
      );
    };

    measure();
    // Re-measure on font load / window resize / container reflow.
    const ro = new ResizeObserver(measure);
    ro.observe(nav);
    return () => ro.disconnect();
  }, [pathname]);

  return (
    <nav
      ref={navRef}
      aria-label="Primary"
      className={cn('relative flex items-center gap-1', className)}
    >
      {pill && (
        <motion.span
          aria-hidden="true"
          className="pointer-events-none absolute left-0 rounded-lg bg-primary/12"
          style={{ top: pill.top, height: pill.height }}
          initial={false}
          animate={{ x: pill.left, width: pill.width }}
          transition={
            reduceMotion
              ? { duration: 0.12, ease: 'easeOut' }
              : { type: 'spring', stiffness: 480, damping: 40, mass: 0.6 }
          }
        />
      )}

      {navItems.map((item) => {
        const exact = 'exact' in item && item.exact;
        const active = exact ? pathname === item.href : pathname.startsWith(item.href);

        return (
          <Link
            key={item.href}
            href={item.href}
            data-nav-active={active}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'relative z-10 shrink-0 rounded-lg text-sm font-medium transition-colors',
              pad,
              active ? 'text-primary' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(item.key)}
          </Link>
        );
      })}
    </nav>
  );
}

// Routes that render their own full-screen layout, without the app nav.
const bareRoutes = new Set(['/login', '/register', '/extension-auth']);
// Public pages that get a reduced bar (brand + toggles) when there is no session.
const infoRoutes = new Set(['/about', '/credits', '/privacy']);

/** The leaf logo and "TaleTrack" wordmark, linking home. */
function Brand() {
  return (
    <Link href="/" className="flex shrink-0 items-center gap-2">
      <Leaf aria-hidden="true" className="size-5 text-primary" />
      <span className="font-heading text-2xl font-semibold leading-none">TaleTrack</span>
    </Link>
  );
}

/**
 * Sticky top bar: logo, main navigation, language and theme toggles, and the account menu.
 *
 * @remarks
 * Mounted once in the root layout so it survives client navigations, which the sliding
 * highlight needs. Hidden on the full-screen auth pages; without a session it only appears,
 * reduced to logo and toggles, on the public info pages.
 *
 * @param props - Component props.
 * @param props.authed - Whether the request had a valid session cookie. It comes from the
 * server so the first paint already shows the right bar.
 * @param props.avatarUrl - The user's avatar for the account menu button.
 */
export default function TopNav({
  authed,
  avatarUrl,
}: {
  authed: boolean;
  avatarUrl?: string | null;
}) {
  const pathname = usePathname();
  const t = useT();
  const { user, isAuthenticated, logout } = useAuth();
  const menuRef = useRef<HTMLDetailsElement>(null);

  // Close the account menu on outside click.
  useEffect(() => {
    const onPointerDown = (e: PointerEvent) => {
      const el = menuRef.current;
      if (el?.open && !el.contains(e.target as Node)) el.removeAttribute('open');
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, []);

  if (bareRoutes.has(pathname)) return null;

  if (!authed) {
    if (!infoRoutes.has(pathname)) return null;
    return (
      <header className="sticky top-0 z-30 border-b border-border bg-card/80 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 lg:px-6">
          <Brand />
          <div className="flex items-center gap-2">
            <LocaleToggle />
            <ThemeToggle compact />
          </div>
        </div>
      </header>
    );
  }

  const handleLogout = () => {
    menuRef.current?.removeAttribute('open');
    logout();
    // Full document load: `/` renders the home or the public landing depending on the cookie.
    window.location.assign('/');
  };

  return (
    <header className="sticky top-0 z-30 border-b border-border bg-card/80 backdrop-blur-md">
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4 lg:px-6">
        <Brand />

        {/* Primary nav */}
        <NavItems className="hidden sm:flex" />

        <div className="flex flex-1 items-center justify-end gap-2">
          <LocaleToggle />
          <ThemeToggle compact />

          {/* User menu */}
          {isAuthenticated && (
            <details ref={menuRef} className="group relative">
              <summary
                className="flex cursor-pointer list-none items-center rounded-full select-none [&::-webkit-details-marker]:hidden"
                aria-label={t('nav.accountMenu')}
              >
                <UserAvatar username={user?.username ?? 'U'} avatarUrl={avatarUrl} size="sm" />
              </summary>
              <div className="absolute right-0 mt-2 w-52 overflow-hidden rounded-xl border border-border bg-popover p-1 shadow-lg">
                <div className="px-3 py-2">
                  <p className="truncate text-sm font-medium text-foreground">{user?.username}</p>
                  <p className="truncate text-xs text-muted-foreground">{user?.email}</p>
                </div>
                <div className="my-1 h-px bg-border" />
                <Link
                  href="/profile"
                  onClick={() => menuRef.current?.removeAttribute('open')}
                  className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                >
                  <User aria-hidden="true" className="size-4" />
                  {t('nav.viewProfile')}
                </Link>
                <Link
                  href="/friends"
                  onClick={() => menuRef.current?.removeAttribute('open')}
                  className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                >
                  <Users aria-hidden="true" className="size-4" />
                  {t('nav.friends')}
                </Link>
                <Button
                  type="button"
                  variant="ghost"
                  onClick={handleLogout}
                  className="h-auto w-full justify-start gap-2 px-3 py-2 font-normal text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                >
                  <LogOut aria-hidden="true" className="size-4" />
                  {t('nav.logout')}
                </Button>
              </div>
            </details>
          )}
        </div>
      </div>

      {/* Mobile nav row */}
      <NavItems
        className="overflow-x-auto border-t border-border px-4 py-1.5 sm:hidden"
        compact
      />
    </header>
  );
}
