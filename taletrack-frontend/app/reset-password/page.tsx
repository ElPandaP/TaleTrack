'use client';

import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { Lock, ArrowRight, Eye, EyeOff } from 'lucide-react';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { useI18n } from '@/lib/i18n';
import AuthShell from '@/app/_components/auth-shell';

function ResetPasswordForm() {
  const { t } = useI18n();
  const router = useRouter();
  const token = useSearchParams().get('token') ?? '';

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [show, setShow] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const tooShort = password.length > 0 && password.length < 6;
  const mismatch = confirm.length > 0 && confirm !== password;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (password.length < 6 || password !== confirm) return;
    setSubmitting(true);
    setError(null);
    try {
      await authService.resetPassword(token, password);
      router.replace('/login?reset=1');
    } catch (err) {
      const code = err instanceof ApiError ? err.code : undefined;
      setError(t(code === 'invalid_or_expired' ? 'auth.reset.expired' : 'auth.reset.error'));
      setSubmitting(false);
    }
  };

  if (!token) {
    return (
      <>
        <h1 className="font-heading text-2xl font-semibold">{t('auth.reset.title')}</h1>
        <p className="mt-2 text-sm text-destructive">{t('auth.reset.noToken')}</p>
        <Link href="/forgot-password" className="mt-4 inline-block text-sm text-primary hover:underline">
          {t('auth.reset.requestNew')}
        </Link>
      </>
    );
  }

  return (
    <>
      <h1 className="font-heading text-2xl font-semibold">{t('auth.reset.title')}</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">{t('auth.reset.subtitle')}</p>

      {error && <p className="mb-4 text-sm text-destructive">{error}</p>}

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <label className="flex flex-col gap-1.5 text-sm font-medium">
          {t('auth.reset.newPassword')}
          <div className="relative">
            <Lock className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <input
              type={show ? 'text' : 'password'}
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-xl border border-border bg-background py-2.5 pr-10 pl-9 text-sm outline-none focus:border-primary"
            />
            <button
              type="button"
              onClick={() => setShow((s) => !s)}
              aria-label={show ? t('auth.hidePassword') : t('auth.showPassword')}
              className="absolute top-1/2 right-3 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            >
              {show ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </button>
          </div>
          {tooShort && <span className="text-xs text-destructive">{t('auth.reset.tooShort')}</span>}
        </label>

        <label className="flex flex-col gap-1.5 text-sm font-medium">
          {t('auth.confirmPassword')}
          <div className="relative">
            <Lock className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <input
              type={show ? 'text' : 'password'}
              required
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              className="w-full rounded-xl border border-border bg-background py-2.5 pr-3 pl-9 text-sm outline-none focus:border-primary"
            />
          </div>
          {mismatch && <span className="text-xs text-destructive">{t('auth.register.passwordsMismatch')}</span>}
        </label>

        <button
          type="submit"
          disabled={submitting || password.length < 6 || password !== confirm}
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-primary py-3 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90 disabled:opacity-50"
        >
          {submitting ? t('auth.reset.saving') : t('auth.reset.submit')}
          {!submitting && <ArrowRight className="size-4" />}
        </button>
      </form>
    </>
  );
}

export default function ResetPasswordPage() {
  return (
    <AuthShell>
      <Suspense fallback={<div className="py-6 text-center text-sm text-muted-foreground">…</div>}>
        <ResetPasswordForm />
      </Suspense>
    </AuthShell>
  );
}
