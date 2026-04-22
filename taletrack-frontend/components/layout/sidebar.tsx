'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import {
  BookOpen, Film, Tv, LayoutDashboard, User,
  LogOut, BookMarked, Menu, X, Leaf,
} from 'lucide-react';
import { useState } from 'react';
import { useAuth } from '@/lib/auth-context';
import ThemeToggle from './theme-toggle';

const navItems = [
  { href: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { href: '/books',     label: 'Books',     icon: BookOpen  },
  { href: '/movies',    label: 'Movies',    icon: Film      },
  { href: '/series',    label: 'Series',    icon: Tv        },
  { href: '/comics',    label: 'Comics',    icon: BookMarked },
];

function Avatar({ username }: { username: string }) {
  const initials = username.split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase();
  return (
    <div className="w-8 h-8 rounded-full bg-primary/20 border border-primary/30 flex items-center justify-center text-xs font-semibold text-primary shrink-0">
      {initials}
    </div>
  );
}

function SidebarContent({ onClose }: { onClose?: () => void }) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout } = useAuth();

  const handleLogout = () => {
    logout();
    router.push('/');
    onClose?.();
  };

  return (
    <div className="flex flex-col h-full">
      {/* Logo */}
      <div className="px-4 py-5 flex items-center justify-between">
        <Link href="/dashboard" onClick={onClose} className="flex items-center gap-2.5 group">
          <div className="w-8 h-8 bg-primary/15 border border-primary/25 rounded-xl flex items-center justify-center group-hover:bg-primary/25 transition-colors">
            <Leaf className="w-4 h-4 text-primary" />
          </div>
          <span className="font-heading font-semibold text-[17px] text-foreground tracking-tight">
            TaleTrack
          </span>
        </Link>
        {onClose && (
          <button onClick={onClose} aria-label="Close sidebar"
            className="p-1.5 rounded-lg text-muted-foreground hover:text-foreground hover:bg-secondary transition-colors cursor-pointer lg:hidden">
            <X className="w-4 h-4" />
          </button>
        )}
      </div>

      <div className="px-4 mb-1">
        <p className="text-[10px] font-semibold text-muted-foreground/60 uppercase tracking-widest">Library</p>
      </div>

      {/* Nav */}
      <nav className="flex-1 px-3 space-y-0.5">
        {navItems.map(({ href, label, icon: Icon }) => {
          const active = pathname === href || pathname.startsWith(href + '/');
          return (
            <Link key={href} href={href} onClick={onClose}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-150 cursor-pointer ${
                active
                  ? 'bg-primary/12 text-primary border border-primary/15'
                  : 'text-muted-foreground hover:text-foreground hover:bg-secondary/60'
              }`}>
              <Icon className={`w-4 h-4 shrink-0 ${active ? 'text-primary' : ''}`} />
              {label}
              {active && <div className="ml-auto w-1.5 h-1.5 rounded-full bg-primary" />}
            </Link>
          );
        })}
      </nav>

      {/* Bottom */}
      <div className="px-3 pb-4 pt-2 border-t border-border mt-2 space-y-1">
        <Link href="/profile" onClick={onClose}
          className={`flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all cursor-pointer ${
            pathname === '/profile'
              ? 'bg-primary/12 text-primary border border-primary/15'
              : 'text-muted-foreground hover:text-foreground hover:bg-secondary/60'
          }`}>
          <User className="w-4 h-4 shrink-0" />
          Profile
        </Link>

        {/* Theme toggle row */}
        <div className="flex items-center justify-between px-3 py-2">
          <span className="text-xs text-muted-foreground">Appearance</span>
          <ThemeToggle />
        </div>

        {/* User */}
        <div className="flex items-center gap-3 px-3 py-2">
          <Avatar username={user?.username ?? 'U'} />
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-foreground truncate">{user?.username ?? 'User'}</p>
            <p className="text-xs text-muted-foreground truncate">{user?.email ?? ''}</p>
          </div>
          <button onClick={handleLogout} aria-label="Log out" title="Log out"
            className="p-1.5 rounded-lg text-muted-foreground hover:text-red-500 hover:bg-red-500/10 transition-colors cursor-pointer shrink-0">
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}

export default function Sidebar() {
  const [mobileOpen, setMobileOpen] = useState(false);
  return (
    <>
      <button onClick={() => setMobileOpen(true)} aria-label="Open sidebar"
        className="lg:hidden fixed top-4 left-4 z-40 p-2 rounded-xl bg-card border border-border text-muted-foreground hover:text-foreground shadow-sm transition-colors cursor-pointer">
        <Menu className="w-5 h-5" />
      </button>

      {mobileOpen && (
        <div className="lg:hidden fixed inset-0 z-40 bg-foreground/20 backdrop-blur-sm"
          onClick={() => setMobileOpen(false)} />
      )}

      <div className={`lg:hidden fixed top-0 left-0 z-50 h-full w-60 bg-sidebar border-r border-sidebar-border transition-transform duration-300 ${mobileOpen ? 'translate-x-0' : '-translate-x-full'}`}>
        <SidebarContent onClose={() => setMobileOpen(false)} />
      </div>

      <aside className="hidden lg:flex flex-col w-60 shrink-0 bg-sidebar border-r border-sidebar-border h-screen sticky top-0">
        <SidebarContent />
      </aside>
    </>
  );
}
