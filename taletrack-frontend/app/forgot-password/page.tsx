'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Mail, ArrowRight, ArrowLeft, Check } from 'lucide-react';
import { authService } from '@/lib/api/services';
import { useI18n } from '@/lib/i18n';
import AuthShell from '@/app/_components/auth-shell';

export default function ForgotPasswordPage() {
  const { t, locale } = useI18n();
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      await authService.requestPasswordReset(email, locale);
    } catch {
      /* response is intentionally identical whether or not the account exists */
    } finally {
      setSubmitting(false);
      setSent(true);
    }
  };

  if (sent) {
    return (
      <AuthShell>
        <div className="flex flex-col items-center gap-3 py-2 text-center">
          <span className="flex size-10 items-center justify-center rounded-full bg-primary/15 text-primary">
            <Check className="size-5" />
          </span>
          <h1 className="font-heading text-xl font-semibold">{t('auth.forgot.sentTitle')}</h1>
          <p className="text-sm text-muted-foreground">{t('auth.forgot.sentBody')}</p>
          <Link href="/login" className="mt-2 inline-flex items-center gap-1.5 text-sm text-primary hover:underline">
            <ArrowLeft className="size-4" /> {t('auth.forgot.backToLogin')}
          </Link>
        </div>
      </AuthShell>
    );
  }

  return (
    <AuthShell>
      <h1 className="font-heading text-2xl font-semibold">{t('auth.forgot.title')}</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">{t('auth.forgot.subtitle')}</p>

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <label className="flex flex-col gap-1.5 text-sm font-medium">
          {t('auth.email')}
          <div className="relative">
            <Mail className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder={t('auth.emailPlaceholder')}
              className="w-full rounded-xl border border-border bg-background py-2.5 pr-3 pl-9 text-sm outline-none focus:border-primary"
            />
          </div>
        </label>

        <button
          type="submit"
          disabled={submitting}
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-primary py-3 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90 disabled:opacity-50"
        >
          {submitting ? t('auth.forgot.sending') : t('auth.forgot.submit')}
          {!submitting && <ArrowRight className="size-4" />}
        </button>
      </form>

      <Link href="/login" className="mt-6 inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" /> {t('auth.forgot.backToLogin')}
      </Link>
    </AuthShell>
  );
}
