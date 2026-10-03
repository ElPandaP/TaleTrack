'use client';

import { CheckCircle } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { checkPassword } from '@/lib/password';

/**
 * Live hints under a new-password field: one per part of the password rule, highlighted once
 * the password meets it. Renders nothing while the field is empty.
 *
 * @param props - Component props.
 * @param props.password - The password being typed.
 */
export function PasswordChecklist({ password }: { password: string }) {
  const t = useT();
  if (!password) return null;
  const c = checkPassword(password);
  const checks = [
    { label: t('auth.register.check.chars'), ok: c.length },
    { label: t('auth.register.check.upper'), ok: c.upper },
    { label: t('auth.register.check.number'), ok: c.number },
  ];
  return (
    <div className="flex gap-3 mt-2">
      {checks.map((check) => (
        <div
          key={check.label}
          className={`flex items-center gap-1 text-xs ${check.ok ? 'text-[oklch(0.52_0.09_152)]' : 'text-muted-foreground/50'}`}
        >
          <CheckCircle className="w-3 h-3" />
          {check.label}
        </div>
      ))}
    </div>
  );
}
