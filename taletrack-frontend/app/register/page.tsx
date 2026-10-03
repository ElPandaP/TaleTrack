'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { GoogleLogin } from '@react-oauth/google';
import { Mail, Lock, User, ArrowRight, Eye, EyeOff, CheckCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { useAuth } from '@/lib/auth-context';
import { useI18n } from '@/lib/i18n';
import { isPasswordValid, PASSWORD_MAX_LENGTH } from '@/lib/password';
import { PasswordChecklist } from '@/components/auth/PasswordChecklist';
import AuthShell from '@/components/auth/AuthShell';
import { useGoogleSignIn } from '@/components/auth/useGoogleSignIn';

/**
 * `/register`: sign-up form (signs in right after creating the account) plus Google sign-up,
 * including the follow-up screens for choosing a username and a linked account.
 */
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

  useEffect(() => {
    if (!loading && isAuthenticated) router.replace('/');
  }, [isAuthenticated, loading, router]);

  // `/` renders a different tree depending on the auth cookie (landing or home), so an
  // auth transition needs a full document load, not a client navigation.
  const goHome = () => {
    window.location.assign('/');
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isPasswordValid(password)) return;
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
            login();
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

  const { handleGoogle, followUp } = useGoogleSignIn({
    failureMessage: t('auth.googleSignUpFailed'),
    onError: setError,
    onBusy: setSubmitting,
    onSignedIn: goHome,
  });

  return (
    <AuthShell card={false} accent="end">
      {followUp ?? (
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
                  maxLength={PASSWORD_MAX_LENGTH}
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
              <PasswordChecklist password={password} />
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

            <Button
              type="submit"
              disabled={submitting || !isPasswordValid(password) || (!!confirm && confirm !== password)}
              className="h-auto w-full py-3"
            >
              {submitting ? (
                <Spinner />
              ) : (
                <>
                  {t('auth.register.submit')} <ArrowRight className="w-4 h-4" />
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

          <p className="mt-6 text-center text-xs text-muted-foreground">
            {t('auth.register.privacyNote')}{' '}
            <Link href="/privacy" className="text-primary hover:text-primary/80 transition-colors">
              {t('auth.register.privacyLink')}
            </Link>
            .
          </p>
        </div>

        <p className="text-center text-sm text-muted-foreground mt-6">
          {t('auth.register.haveAccount')}{' '}
          <Link href="/login" className="text-primary hover:text-primary/80 font-medium transition-colors">
            {t('auth.register.signIn')}
          </Link>
        </p>
        </>
      )}
    </AuthShell>
  );
}
