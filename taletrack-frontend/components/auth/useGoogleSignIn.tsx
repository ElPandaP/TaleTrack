'use client';

import { useState } from 'react';
import { authService } from '@/lib/api/services';
import { useAuth, parseJwt } from '@/lib/auth-context';
import { useI18n } from '@/lib/i18n';
import { ChooseUsernameScreen } from '@/components/auth/ChooseUsernameScreen';
import { LinkedAccountNotice } from '@/components/auth/LinkedAccountNotice';

/** Callbacks and texts a page gives {@link useGoogleSignIn}. */
export interface GoogleSignInOptions {
  /** Error shown when Google sign-in fails or the pending sign-up expires. */
  failureMessage: string;
  /** Shows an error on the page, or clears it with `null`. */
  onError: (message: string | null) => void;
  /** Marks the page as busy while the backend answers. */
  onBusy: (busy: boolean) => void;
  /** Runs once the session is stored, to leave the page. */
  onSignedIn: () => void;
}

/**
 * Google sign-in shared by the login and register pages. A known account signs in straight
 * away; a new one first picks a username, and an existing email account that just got linked
 * sees a notice before continuing.
 *
 * @param options - See {@link GoogleSignInOptions}.
 * @returns `handleGoogle`, to call with the credential from Google's button, and `followUp`, the
 * username or linked-account screen the page must show instead of its form (null when none).
 */
export function useGoogleSignIn({ failureMessage, onError, onBusy, onSignedIn }: GoogleSignInOptions) {
  const { login } = useAuth();
  const { locale } = useI18n();
  const [pending, setPending] = useState<{
    pendingToken: string;
    suggestedUsername: string;
    email: string;
  } | null>(null);
  const [linkedUsername, setLinkedUsername] = useState<string | null>(null);

  const finish = () => {
    login();
    onSignedIn();
  };

  const handleGoogle = async (credential: string) => {
    onBusy(true);
    onError(null);
    try {
      const res = await authService.googleLogin(credential);
      if ('needsUsername' in res) {
        setPending({
          pendingToken: res.pendingToken,
          suggestedUsername: res.suggestedUsername,
          email: res.email,
        });
        return;
      }
      if (res.success && res.token) {
        if (res.linkedExistingAccount) {
          setLinkedUsername(parseJwt(res.token)?.unique_name ?? 'User');
        } else {
          finish();
        }
      }
    } catch {
      onError(failureMessage);
    } finally {
      onBusy(false);
    }
  };

  const followUp = pending ? (
    <ChooseUsernameScreen
      pendingToken={pending.pendingToken}
      suggestedUsername={pending.suggestedUsername}
      email={pending.email}
      locale={locale}
      onDone={finish}
      onExpired={() => {
        setPending(null);
        onError(failureMessage);
      }}
    />
  ) : linkedUsername ? (
    <LinkedAccountNotice username={linkedUsername} onContinue={finish} />
  ) : null;

  return { handleGoogle, followUp };
}
