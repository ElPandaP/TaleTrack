'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import { motion, useReducedMotion } from 'framer-motion';
import { Leaf, User, LogOut, Users } from 'lucide-react';
import { useAuth } from '@/lib/auth-context';
import { useT } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import { UserAvatar } from '@/components/media/user-avatar';
import ThemeToggle from './theme-toggle';
import LocaleToggle from './locale-toggle';

const navItems = [
  { href: '/', key: 'nav.home', exact: true },
  { href: '/library', key: 'nav.library' },
  { href: '/reviews', key: 'nav.reviews' },
  { href: '/activity', key: 'nav.activity' },
  { href: '/connections', key: 'nav.connections' },
];

type PillRect = { left: number; top: number; width: number; height: number };

/**
 * Nav links with a single, always-mounted "pill" that animates its position and
 * width to sit under the active link. We measure the active <Link> and drive the
 * pill's `x`/`width` — this survives Next's segment swaps and Suspense (a shared
 * `layoutId` does not), so the pill slides instead of jumping.
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

/**
 * The top navigation bar. Mounted once in the root layout so it survives every
 * client navigation (that persistence is what lets the active-item pill slide).
 * It hides itself on the auth screens and for logged-out visitors — `authed`
 * comes from the server cookie so there's no first-paint flash; login/logout do
 * a full document load, which refreshes it.
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

  if (!authed || bareRoutes.has(pathname)) return null;

  const handleLogout = () => {
    menuRef.current?.removeAttribute('open');
    logout();
    // Full document load: `/` swaps between the home and the public landing.
    window.location.assign('/');
  };

  return (
    <header className="sticky top-0 z-30 border-b border-border bg-card/80 backdrop-blur-md">
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4 lg:px-6">
        {/* Brand */}
        <Link href="/" className="flex shrink-0 items-center gap-2">
          <span className="flex size-7 items-center justify-center rounded-lg border border-primary/25 bg-primary/15">
            <Leaf aria-hidden="true" className="size-4 text-primary" />
          </span>
          <span className="font-heading text-[17px] font-semibold tracking-tight">TaleTrack</span>
        </Link>

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
                  href="/friends"
                  onClick={() => menuRef.current?.removeAttribute('open')}
                  className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                >
                  <Users aria-hidden="true" className="size-4" />
                  {t('nav.friends')}
                </Link>
                <Link
                  href="/profile"
                  onClick={() => menuRef.current?.removeAttribute('open')}
                  className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                >
                  <User aria-hidden="true" className="size-4" />
                  {t('nav.viewProfile')}
                </Link>
                <button
                  type="button"
                  onClick={handleLogout}
                  className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
                >
                  <LogOut aria-hidden="true" className="size-4" />
                  {t('nav.logout')}
                </button>
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
