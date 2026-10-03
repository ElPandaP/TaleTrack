'use client';

import { CheckCircle, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useT } from '@/lib/i18n';

/**
 * Notice shown when a Google sign-in was linked to an existing email and password account
 * with the same email.
 *
 * @param props - Component props.
 * @param props.username - The existing account's username.
 * @param props.onContinue - Called when the user acknowledges the notice.
 */
export function LinkedAccountNotice({
  username,
  onContinue,
}: {
  username: string;
  onContinue: () => void;
}) {
  const t = useT();

  return (
    <div className="tt-card p-8 text-center">
      <div className="mx-auto mb-4 flex size-12 items-center justify-center rounded-full bg-primary/15">
        <CheckCircle className="size-6 text-primary" />
      </div>
      <h1 className="font-heading text-xl font-semibold mb-2">{t('auth.linkedAccount.title')}</h1>
      <p className="text-muted-foreground text-sm mb-6">
        {t('auth.linkedAccount.subtitle', { username })}
      </p>
      <Button type="button" onClick={onContinue} className="h-auto w-full py-3">
        {t('auth.linkedAccount.continue')} <ArrowRight className="w-4 h-4" />
      </Button>
    </div>
  );
}
