'use client';

import { useState } from 'react';
import { User, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { useT } from '@/lib/i18n';

/**
 * Username form shown after a first Google sign-up. The account does not exist yet (the backend
 * only issued a short-lived pending token) and is created once the user picks a username here.
 *
 * @param props - Component props.
 * @param props.pendingToken - Token from the "needs username" Google response.
 * @param props.suggestedUsername - Initial value of the field.
 * @param props.email - The Google account's email, shown in the subtitle.
 * @param props.locale - UI locale, sent so the welcome email uses it.
 * @param props.onDone - Called once the account exists and the tokens are stored.
 * @param props.onExpired - Called when the pending token has expired and the sign-up must start over.
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
  onDone: () => void;
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
        onDone();
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

        <Button
          type="submit"
          disabled={submitting || username.trim().length < 3}
          className="h-auto w-full py-3"
        >
          {submitting ? (
            <Spinner />
          ) : (
            <>
              {t('auth.chooseUsername.submit')} <ArrowRight className="w-4 h-4" />
            </>
          )}
        </Button>
      </form>
    </div>
  );
}
