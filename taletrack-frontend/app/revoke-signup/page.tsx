'use client';

import { authService } from '@/lib/api/services';
import DeleteAccountLink from '@/components/auth/DeleteAccountLink';

/** `/revoke-signup`: target of the "I did not sign up" link in the welcome email; deletes the account after confirmation. */
export default function RevokeSignupPage() {
  return (
    <DeleteAccountLink
      action={(token) => authService.revokeSignup(token)}
      texts={{
        title: 'auth.revokeSignup.title',
        body: 'auth.revokeSignup.body',
        confirm: 'auth.revokeSignup.confirm',
        cancel: 'auth.revokeSignup.keep',
        expired: 'auth.revokeSignup.expired',
        doneBody: 'auth.revokeSignup.doneBody',
      }}
    />
  );
}
