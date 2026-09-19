'use client';

import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { ArrowLeft, ArrowRight, KeyRound } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { useAuth, parseJwt, type AuthUser } from '@/lib/auth-context';
import { useI18n } from '@/lib/i18n';
import AuthShell from '@/components/auth/AuthShell';

function LoginCodeForm() {
  const { t, locale } = useI18n();
  const { login } = useAuth();
  const email = useSearchParams().get('email') ?? '';

  const [code, setCode] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [resending, setResending] = useState(false);
  const [resent, setResent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // `/` renders a different tree depending on the auth cookie (landing vs home),
  // so an auth transition needs a full document load, not a soft navigation.
  const goHome = () => {
    window.location.assign('/');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const res = await authService.verifyLoginCode(email, code);
      if (res.success && res.token) {
        const decoded = parseJwt(res.token);
        const authUser: AuthUser = {
          id: decoded?.sub ?? '',
          username: decoded?.unique_name ?? email.split('@')[0],
          email: decoded?.email ?? email,
        };
        login(authUser);
        goHome();
        return;
      }
    } catch (err) {
      const status = err instanceof ApiError ? err.status : undefined;
      setError(t(status === 401 ? 'auth.code.invalid' : 'auth.loginError'));
    }
    setSubmitting(false);
  };

  const handleResend = async () => {
    setResending(true);
    setResent(false);
    try {
      await authService.requestLoginCode(email, locale);
    } catch {
      /* same anti-enumeration response as the initial request */
    } finally {
      setResending(false);
      setResent(true);
    }
  };

  if (!email) {
    return (
      <>
        <h1 className="font-heading text-2xl font-semibold">{t('auth.code.title')}</h1>
        <p className="mt-2 text-sm text-destructive">{t('auth.code.noEmail')}</p>
        <Link href="/forgot-password" className="mt-4 inline-block text-sm text-primary hover:underline">
          {t('auth.reset.requestNew')}
        </Link>
      </>
    );
  }

  return (
    <>
      <h1 className="font-heading text-2xl font-semibold">{t('auth.code.title')}</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">{t('auth.code.subtitle', { email })}</p>

      {error && <p className="mb-4 text-sm text-destructive">{error}</p>}

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <label className="flex flex-col gap-1.5 text-sm font-medium">
          {t('auth.code.codeLabel')}
          <div className="relative">
            <KeyRound className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <input
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              required
              maxLength={6}
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
              placeholder="000000"
              className="w-full rounded-xl border border-border bg-background py-2.5 pr-3 pl-9 text-sm tracking-[0.3em] outline-none focus:border-primary"
            />
          </div>
        </label>

        <Button type="submit" disabled={submitting || code.length !== 6} className="h-auto w-full py-3">
          {submitting ? <Spinner /> : (
            <>
              {t('auth.login.submit')} <ArrowRight className="size-4" />
            </>
          )}
        </Button>
      </form>

      <div className="mt-4 flex items-center justify-between text-sm">
        <Link href="/login" className="inline-flex items-center gap-1.5 text-muted-foreground hover:text-foreground">
          <ArrowLeft className="size-4" /> {t('auth.forgot.backToLogin')}
        </Link>
        <button
          type="button"
          onClick={handleResend}
          disabled={resending}
          className="cursor-pointer text-primary hover:underline disabled:cursor-not-allowed disabled:opacity-60"
        >
          {resent ? t('auth.code.resent') : t('auth.code.resend')}
        </button>
      </div>
    </>
  );
}

export default function LoginCodePage() {
  return (
    <AuthShell>
      <Suspense fallback={<div className="py-6 text-center text-sm text-muted-foreground">…</div>}>
        <LoginCodeForm />
      </Suspense>
    </AuthShell>
  );
}
