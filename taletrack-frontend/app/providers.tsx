'use client';

import { GoogleOAuthProvider } from '@react-oauth/google';
import { API_CONFIG } from '@/lib/api/config';
import { AuthProvider } from '@/lib/auth-context';
import { ThemeProvider } from '@/lib/theme-context';

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <GoogleOAuthProvider clientId={API_CONFIG.googleClientId} use_fedcm_for_prompt={false}>
      <ThemeProvider>
        <AuthProvider>{children}</AuthProvider>
      </ThemeProvider>
    </GoogleOAuthProvider>
  );
}
