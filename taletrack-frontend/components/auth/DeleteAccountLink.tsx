'use client';

import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Check, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/api/client';
import { useAuth } from '@/lib/auth-context';
import { useI18n } from '@/lib/i18n';
import AuthShell from '@/components/auth/AuthShell';

/** Translation keys of the texts that differ between the pages using {@link DeleteAccountLink}. */
export interface DeleteAccountLinkTexts {
  /** Heading of the confirmation step. */
  title: string;
  /** Explanation under the heading. */
  body: string;
  /** Label of the confirm button. */
  confirm: string;
  /** Label of the link that leaves without deleting. */
  cancel: string;
  /** Error when the link is invalid or has expired. */
  expired: string;
  /** Message once the account is gone. */
  doneBody: string;
}

/**
 * Body of {@link DeleteAccountLink}: reads the `token` query parameter and asks for one more
 * confirmation before deleting.
 */
function DeleteAccountLinkInner({
  action,
  texts,
}: {
  action: (token: string) => Promise<unknown>;
  texts: DeleteAccountLinkTexts;
}) {
  const { t } = useI18n();
  const { logout } = useAuth();
  const token = useSearchParams().get('token') ?? '';
  const [phase, setPhase] = useState<'idle' | 'working' | 'done' | 'error'>('idle');
  const [error, setError] = useState<string | null>(null);

  const run = async () => {
    setPhase('working');
    try {
      await action(token);
      // The account no longer exists, so a session kept in this browser is useless.
      logout();
      setPhase('done');
    } catch (err) {
      const code = err instanceof ApiError ? err.code : undefined;
      setError(t(code === 'invalid_or_expired' ? texts.expired : 'auth.deleteLink.error'));
      setPhase('error');
    }
  };

  if (!token) {
    return <p className="text-sm text-destructive">{t('auth.deleteLink.noToken')}</p>;
  }

  if (phase === 'done') {
    return (
      <div className="flex flex-col items-center gap-3 py-2 text-center">
        <span className="flex size-10 items-center justify-center rounded-full bg-primary/15 text-primary">
          <Check className="size-5" />
        </span>
        <h1 className="font-heading text-xl font-semibold">{t('auth.deleteLink.doneTitle')}</h1>
        <p className="text-sm text-muted-foreground">{t(texts.doneBody)}</p>
        <Link href="/" className="mt-2 text-sm text-primary hover:underline">{t('auth.deleteLink.home')}</Link>
      </div>
    );
  }

  return (
    <>
      <div className="mb-3 flex items-center gap-2 text-destructive">
        <TriangleAlert className="size-5" />
        <h1 className="font-heading text-xl font-semibold">{t(texts.title)}</h1>
      </div>
      <p className="mb-6 text-sm text-muted-foreground">{t(texts.body)}</p>
      {error && <p className="mb-4 text-sm text-destructive">{error}</p>}
      <Button
        type="button"
        variant="destructive"
        onClick={run}
        disabled={phase === 'working'}
        className="h-auto w-full bg-destructive py-3 text-white hover:bg-destructive/90"
      >
        {phase === 'working' ? t('auth.deleteLink.working') : t(texts.confirm)}
      </Button>
      <Link href="/" className="mt-4 inline-block text-sm text-muted-foreground hover:text-foreground">
        {t(texts.cancel)}
      </Link>
    </>
  );
}

/**
 * Page for an emailed link that deletes an account (confirming a deletion request, or undoing a
 * sign-up the owner of the email did not make). It asks for one more confirmation, and once the
 * account is deleted it also ends the session stored in this browser.
 *
 * @param props - Component props.
 * @param props.action - Calls the backend with the link's token.
 * @param props.texts - The page's own translation keys.
 */
export default function DeleteAccountLink({
  action,
  texts,
}: {
  action: (token: string) => Promise<unknown>;
  texts: DeleteAccountLinkTexts;
}) {
  return (
    <AuthShell>
      <Suspense fallback={<div className="py-6 text-center text-sm text-muted-foreground">…</div>}>
        <DeleteAccountLinkInner action={action} texts={texts} />
      </Suspense>
    </AuthShell>
  );
}
