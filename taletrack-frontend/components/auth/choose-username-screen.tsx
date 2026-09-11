'use client';

import { useState } from 'react';
import { User, ArrowRight } from 'lucide-react';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { parseJwt, type AuthUser } from '@/lib/auth-context';
import { useT } from '@/lib/i18n';

/**
 * Shown after a brand-new Google sign-up: the account isn't created yet (the backend only
 * issued a short-lived pending token) until the user picks a username here.
 */
export function ChooseUsernameScreen({
  pendingToken,
  suggestedUsername,
  email,
  locale,
  onDone,
  onExpired,
}: {
  pendingToken: string;
  suggestedUsername: string;
  email: string;
  locale: string;
  onDone: (user: AuthUser) => void;
  onExpired: () => void;
}) {
  const t = useT();
  const [username, setUsername] = useState(suggestedUsername);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const res = await authService.completeGoogleSignup(pendingToken, username.trim(), locale);
      if (res.success && res.token) {
        const decoded = parseJwt(res.token);
        onDone({
          id: parseInt(decoded?.sub ?? '0'),
          username: decoded?.unique_name ?? username.trim(),
          email: decoded?.email ?? email,
        });
      }
    } catch (err) {
      if (err instanceof ApiError && err.code === 'invalid_or_expired') {
        onExpired();
        return;
      }
      const code = err instanceof ApiError ? err.code : undefined;
      setError(t(code === 'username_taken' ? 'auth.register.usernameTaken' : 'auth.googleSignUpFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="tt-card p-8">
      <h1 className="font-heading text-2xl font-semibold mb-1">{t('auth.chooseUsername.title')}</h1>
      <p className="text-muted-foreground text-sm mb-8">
        {t('auth.chooseUsername.subtitle', { email })}
      </p>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-xs font-medium text-muted-foreground mb-1.5">
            {t('auth.username')}
          </label>
          <div className="relative">
            <User className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              minLength={3}
              maxLength={50}
              autoFocus
              autoComplete="username"
              placeholder={t('auth.usernamePlaceholder')}
              className="tt-input pl-10 pr-4 py-3"
            />
          </div>
        </div>

        {error && (
          <div className="p-3 bg-destructive/10 border border-destructive/20 rounded-xl text-destructive text-sm">
            {error}
          </div>
        )}

        <button
          type="submit"
          disabled={submitting || username.trim().length < 3}
          className="w-full flex items-center justify-center gap-2 py-3 bg-primary text-primary-foreground font-medium rounded-xl hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
        >
          {submitting ? (
            <div className="w-4 h-4 border-2 border-primary-foreground/30 border-t-primary-foreground rounded-full animate-spin" />
          ) : (
            <>
              {t('auth.chooseUsername.submit')} <ArrowRight className="w-4 h-4" />
            </>
          )}
        </button>
      </form>
    </div>
  );
}
