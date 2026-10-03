'use client';

import { GoogleOAuthProvider } from '@react-oauth/google';
import { API_CONFIG } from '@/lib/api/config';
import { AuthProvider } from '@/lib/auth-context';
import { ThemeProvider } from '@/lib/theme-context';
import { LocaleProvider, type Locale } from '@/lib/i18n';

/** Client-side context providers shared by every page: Google OAuth, locale, theme and auth. */
export function Providers({
  children,
  locale,
}: {
  children: React.ReactNode;
  locale: Locale;
}) {
  return (
    <GoogleOAuthProvider clientId={API_CONFIG.googleClientId}>
      <LocaleProvider initialLocale={locale}>
        <ThemeProvider>
          <AuthProvider>{children}</AuthProvider>
        </ThemeProvider>
      </LocaleProvider>
    </GoogleOAuthProvider>
  );
}
