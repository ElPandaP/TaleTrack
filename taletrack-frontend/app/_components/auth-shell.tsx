'use client';

import { Leaf } from 'lucide-react';
import LocaleToggle from '@/components/layout/locale-toggle';

/** Centered card used by the email-link pages (reset password, confirm delete, …). */
export default function AuthShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-background px-4">
      <div className="absolute top-4 right-4 z-20">
        <LocaleToggle />
      </div>
      <div className="pointer-events-none absolute top-0 left-1/4 h-96 w-96 rounded-full bg-primary/8 blur-3xl" />
      <div className="relative z-10 w-full max-w-md">
        <div className="mb-8 flex items-center justify-center gap-2.5">
          <div className="flex size-9 items-center justify-center rounded-xl border border-primary/25 bg-primary/15">
            <Leaf className="size-4.5 text-primary" />
          </div>
          <span className="font-heading text-lg font-semibold tracking-tight">TaleTrack</span>
        </div>
        <div className="tt-card p-8">{children}</div>
      </div>
    </div>
  );
}
