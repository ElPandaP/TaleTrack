'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { GoogleLogin } from '@react-oauth/google';
import { Leaf, Mail, Lock, User, ArrowRight, Eye, EyeOff, CheckCircle } from 'lucide-react';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { useAuth, parseJwt, type AuthUser } from '@/lib/auth-context';
import { useT, useI18n } from '@/lib/i18n';
import LocaleToggle from '@/components/layout/locale-toggle';
import { ChooseUsernameScreen } from '@/components/auth/choose-username-screen';
import { LinkedAccountNotice } from '@/components/auth/linked-account-notice';

function PasswordStrength({ password }: { password: string }) {
  const t = useT();
  const checks = [
    { label: t('auth.register.check.chars'), ok: password.length >= 8 },
    { label: t('auth.register.check.upper'), ok: /[A-Z]/.test(password) },
    { label: t('auth.register.check.number'), ok: /\d/.test(password) },
  ];
  if (!password) return null;
  return (
    <div className="flex gap-3 mt-2">
      {checks.map((c) => (
        <div
          key={c.label}
          className={`flex items-center gap-1 text-xs ${c.ok ? 'text-[oklch(0.52_0.09_152)]' : 'text-muted-foreground/50'}`}
        >
          <CheckCircle className="w-3 h-3" />
          {c.label}
        </div>
      ))}
    </div>
  );
}

export default function RegisterPage() {
  const router = useRouter();
  const { t, locale } = useI18n();
  const { login, isAuthenticated, loading } = useAuth();
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [googlePending, setGooglePending] = useState<{
    pendingToken: string;
    suggestedUsername: string;
    email: string;
  } | null>(null);
  const [linkedUser, setLinkedUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    if (!loading && isAuthenticated) router.replace('/');
  }, [isAuthenticated, loading, router]);

  // `/` renders a different tree depending on the auth cookie (landing vs home),
  // so an auth transition needs a full document load, not a soft navigation.
  const goHome = () => {
    window.location.assign('/');
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    if (password !== confirm) {
      setError(t('auth.register.passwordsMismatch'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const res = await authService.register(email, username, password, locale);
      if (res.success) {
        setSuccess(true);
        try {
          const loginRes = await authService.login(email, password);
          if (loginRes.success && loginRes.token) {
            const decoded = parseJwt(loginRes.token);
            login({
              id: parseInt(decoded?.sub ?? '0'),
              username: decoded?.unique_name ?? username,
              email: decoded?.email ?? email,
            });
            goHome();
          }
        } catch {
          router.push('/login');
        }
      }
    } catch (err) {
      const code = err instanceof ApiError ? err.code : undefined;
      const key =
        code === 'email_taken'
          ? 'auth.register.emailTaken'
          : code === 'username_taken'
            ? 'auth.register.usernameTaken'
            : 'auth.register.failed';
      setError(t(key));
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
      setError(t('auth.googleSignUpFailed'));
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
      <div className="absolute top-0 right-1/4 w-96 h-96 bg-primary/8 rounded-full blur-3xl pointer-events-none" />
      <div className="absolute bottom-0 left-1/4 w-80 h-80 bg-[oklch(0.65_0.13_65)]/8 rounded-full blur-3xl pointer-events-none" />

      <div className="w-full max-w-md relative z-10 py-10">
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
              setError(t('auth.googleSignUpFailed'));
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
          <h1 className="font-heading text-2xl font-semibold mb-1">{t('auth.register.title')}</h1>
          <p className="text-muted-foreground text-sm mb-8">{t('auth.register.subtitle')}</p>

          <form onSubmit={handleRegister} className="space-y-4">
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
                  autoComplete="username"
                  placeholder={t('auth.usernamePlaceholder')}
                  className="tt-input pl-10 pr-4 py-3"
                />
              </div>
            </div>

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
              <label className="block text-xs font-medium text-muted-foreground mb-1.5">
                {t('auth.password')}
              </label>
              <div className="relative">
                <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  autoComplete="new-password"
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
              <PasswordStrength password={password} />
            </div>

            <div>
              <label className="block text-xs font-medium text-muted-foreground mb-1.5">
                {t('auth.confirmPassword')}
              </label>
              <div className="relative">
                <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={confirm}
                  onChange={(e) => setConfirm(e.target.value)}
                  required
                  autoComplete="new-password"
                  placeholder="••••••••"
                  className={`tt-input pl-10 pr-4 py-3 ${
                    confirm && confirm !== password ? 'border-destructive/40 focus:border-destructive/60' : ''
                  }`}
                />
              </div>
              {confirm && confirm !== password && (
                <p className="text-xs text-destructive mt-1">{t('auth.register.passwordsMismatch')}</p>
              )}
            </div>

            {error && (
              <div className="p-3 bg-destructive/10 border border-destructive/20 rounded-xl text-destructive text-sm">
                {error}
              </div>
            )}

            {success && (
              <div className="p-3 bg-[oklch(0.52_0.09_152)]/10 border border-[oklch(0.52_0.09_152)]/20 rounded-xl text-[oklch(0.52_0.09_152)] text-sm flex items-center gap-2">
                <CheckCircle className="w-4 h-4" />
                {t('auth.register.created')}
              </div>
            )}

            <button
              type="submit"
              disabled={submitting || (!!confirm && confirm !== password)}
              className="w-full flex items-center justify-center gap-2 py-3 bg-primary text-primary-foreground font-medium rounded-xl hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
            >
              {submitting ? (
                <div className="w-4 h-4 border-2 border-primary-foreground/30 border-t-primary-foreground rounded-full animate-spin" />
              ) : (
                <>
                  {t('auth.register.submit')} <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>

          <div className="relative my-6">
            <div className="absolute inset-0 flex items-center">
              <div className="w-full border-t border-border" />
            </div>
            <div className="relative flex justify-center">
              <span className="bg-card px-3 text-xs text-muted-foreground">
                {t('auth.register.orSignUp')}
              </span>
            </div>
          </div>

          <div className="flex justify-center">
            <GoogleLogin
              onSuccess={(res) => res.credential && handleGoogle(res.credential)}
              onError={() => setError(t('auth.googleSignUpFailed'))}
              theme="outline"
              shape="pill"
              size="large"
            />
          </div>
        </div>

        <p className="text-center text-sm text-muted-foreground mt-6">
          {t('auth.register.haveAccount')}{' '}
          <Link href="/login" className="text-primary hover:text-primary/80 font-medium transition-colors">
            {t('auth.register.signIn')}
          </Link>
        </p>
        </>
        )}
      </div>
    </div>
  );
}
