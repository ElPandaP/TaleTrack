'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

/** Only allow same-origin relative paths as a post-login redirect (no open redirect). */
function safeNext(): string {
  if (typeof window === 'undefined') return '/';
  const raw = new URLSearchParams(window.location.search).get('next');
  return raw && raw.startsWith('/') && !raw.startsWith('//') ? raw : '/';
}
import { GoogleLogin } from '@react-oauth/google';
import { Leaf, Mail, Lock, ArrowRight, Eye, EyeOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { useAuth, parseJwt, type AuthUser } from '@/lib/auth-context';
import { useI18n } from '@/lib/i18n';
import LocaleToggle from '@/components/layout/locale-toggle';
import { ChooseUsernameScreen } from '@/components/auth/choose-username-screen';
import { LinkedAccountNotice } from '@/components/auth/linked-account-notice';

export default function LoginPage() {
  const router = useRouter();
  const { t, locale } = useI18n();
  const { login, isAuthenticated, loading } = useAuth();
  const [justReset, setJustReset] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [googlePending, setGooglePending] = useState<{
    pendingToken: string;
    suggestedUsername: string;
    email: string;
  } | null>(null);
  const [linkedUser, setLinkedUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    if (!loading && isAuthenticated) router.replace(safeNext());
  }, [isAuthenticated, loading, router]);

  useEffect(() => {
    setJustReset(new URLSearchParams(window.location.search).get('reset') === '1');
  }, []);

  // `/` renders a different tree depending on the auth cookie (landing vs home),
  // so an auth transition needs a full document load, not a soft navigation.
  // `next` (e.g. the extension-auth flow) also needs the fresh cookie server-side.
  const goHome = () => {
    window.location.assign(safeNext());
  };

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const res = await authService.login(email, password);
      if (res.success && res.token) {
        const decoded = parseJwt(res.token);
        login({
          id: parseInt(decoded?.sub ?? '0'),
          username: decoded?.unique_name ?? email.split('@')[0],
          email: decoded?.email ?? email,
        });
        goHome();
      }
    } catch (err) {
      const code = err instanceof ApiError ? err.code : undefined;
      setError(t(code === 'invalid_credentials' ? 'auth.invalidCredentials' : 'auth.loginError'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleGoogle = async (credential: string) => {
    setSubmitting(true);
    setError(null);
    try {
      const res = await authService.googleLogin(credential, locale);
      if ('needsUsername' in res) {
        setGooglePending({
          pendingToken: res.pendingToken,
          suggestedUsername: res.suggestedUsername,
          email: res.email,
        });
        return;
      }
      if (res.success && res.token) {
        const decoded = parseJwt(res.token);
        const authUser: AuthUser = {
          id: parseInt(decoded?.sub ?? '0'),
          username: decoded?.unique_name ?? 'User',
          email: decoded?.email ?? '',
        };
        if (res.linkedExistingAccount) {
          setLinkedUser(authUser);
        } else {
          login(authUser);
          goHome();
        }
      }
    } catch {
      setError(t('auth.googleSignInFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-background flex items-center justify-center px-4 relative overflow-hidden">
      <div className="absolute top-4 right-4 z-20">
        <LocaleToggle />
      </div>
      {/* Ambient blobs */}
      <div className="absolute top-0 left-1/4 w-96 h-96 bg-primary/8 rounded-full blur-3xl pointer-events-none" />
      <div className="absolute bottom-0 right-1/4 w-80 h-80 bg-[oklch(0.65_0.13_65)]/8 rounded-full blur-3xl pointer-events-none" />

      <div className="w-full max-w-md relative z-10">
        <div className="flex items-center justify-center gap-2.5 mb-8">
          <div className="w-9 h-9 bg-primary/15 border border-primary/25 rounded-xl flex items-center justify-center">
            <Leaf className="w-4.5 h-4.5 text-primary" />
          </div>
          <span className="font-heading font-semibold text-lg tracking-tight">TaleTrack</span>
        </div>

        {googlePending ? (
          <ChooseUsernameScreen
            pendingToken={googlePending.pendingToken}
            suggestedUsername={googlePending.suggestedUsername}
            email={googlePending.email}
            locale={locale}
            onDone={(authUser) => {
              login(authUser);
              goHome();
            }}
            onExpired={() => {
              setGooglePending(null);
              setError(t('auth.googleSignInFailed'));
            }}
          />
        ) : linkedUser ? (
          <LinkedAccountNotice
            username={linkedUser.username}
            onContinue={() => {
              login(linkedUser);
              goHome();
            }}
          />
        ) : (
        <>
        <div className="tt-card p-8">
          <h1 className="font-heading text-2xl font-semibold mb-1">{t('auth.login.title')}</h1>
          <p className="text-muted-foreground text-sm mb-8">{t('auth.login.subtitle')}</p>

          {justReset && (
            <div className="mb-6 p-3 bg-primary/10 border border-primary/20 rounded-xl text-sm text-foreground">
              {t('auth.reset.doneNotice')}
            </div>
          )}

          <form onSubmit={handleLogin} className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-muted-foreground mb-1.5">
                {t('auth.email')}
              </label>
              <div className="relative">
                <Mail className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  autoComplete="email"
                  placeholder={t('auth.emailPlaceholder')}
                  className="tt-input pl-10 pr-4 py-3"
                />
              </div>
            </div>

            <div>
              <div className="flex items-baseline justify-between mb-1.5">
                <label className="block text-xs font-medium text-muted-foreground">
                  {t('auth.password')}
                </label>
                <Link
                  href="/forgot-password"
                  className="text-xs text-primary hover:text-primary/80 transition-colors"
                >
                  {t('auth.login.forgotPassword')}
                </Link>
              </div>
              <div className="relative">
                <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  autoComplete="current-password"
                  placeholder="••••••••"
                  className="tt-input pl-10 pr-11 py-3"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-3.5 top-1/2 -translate-y-1/2 text-muted-foreground/50 hover:text-muted-foreground transition-colors cursor-pointer"
                  aria-label={showPassword ? t('auth.hidePassword') : t('auth.showPassword')}
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            {error && (
              <div className="p-3 bg-destructive/10 border border-destructive/20 rounded-xl text-destructive text-sm">
                {error}
              </div>
            )}

            <Button type="submit" disabled={submitting} className="h-auto w-full py-3">
              {submitting ? (
                <Spinner />
              ) : (
                <>
                  {t('auth.login.submit')} <ArrowRight className="w-4 h-4" />
                </>
              )}
            </Button>
          </form>

          <div className="relative my-6">
            <div className="absolute inset-0 flex items-center">
              <div className="w-full border-t border-border" />
            </div>
            <div className="relative flex justify-center">
              <span className="bg-card px-3 text-xs text-muted-foreground">
                {t('auth.login.orContinue')}
              </span>
            </div>
          </div>

          <div className="flex justify-center">
            <GoogleLogin
              onSuccess={(res) => res.credential && handleGoogle(res.credential)}
              onError={() => setError(t('auth.googleSignInFailed'))}
              theme="outline"
              shape="pill"
              size="large"
            />
          </div>
        </div>

        <p className="text-center text-sm text-muted-foreground mt-6">
          {t('auth.login.noAccount')}{' '}
          <Link
            href="/register"
            className="text-primary hover:text-primary/80 font-medium transition-colors"
          >
            {t('auth.login.createOne')}
          </Link>
        </p>
        </>
        )}
      </div>
    </div>
  );
}
