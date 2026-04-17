'use client';

import { GoogleOAuthProvider } from '@react-oauth/google';
import { API_CONFIG } from '@/lib/api/config';

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <GoogleOAuthProvider clientId={API_CONFIG.googleClientId}>
      {children}
    </GoogleOAuthProvider>
  );
}
