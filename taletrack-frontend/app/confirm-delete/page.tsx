'use client';

import { authService } from '@/lib/api/services';
import DeleteAccountLink from '@/components/auth/DeleteAccountLink';

/** `/confirm-delete`: target of the account deletion email link; deletes the account after one more confirmation. */
export default function ConfirmDeletePage() {
  return (
    <DeleteAccountLink
      action={(token) => authService.confirmAccountDeletion(token)}
      texts={{
        title: 'auth.confirmDelete.title',
        body: 'auth.confirmDelete.body',
        confirm: 'auth.confirmDelete.confirm',
        cancel: 'auth.confirmDelete.cancel',
        expired: 'auth.confirmDelete.expired',
        doneBody: 'auth.confirmDelete.doneBody',
      }}
    />
  );
}
