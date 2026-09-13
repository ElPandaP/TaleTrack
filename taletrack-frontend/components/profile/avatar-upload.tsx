'use client';

import { useRef, useState } from 'react';
import { Upload, Trash2 } from 'lucide-react';
import { UserAvatar } from '@/components/media/user-avatar';
import { Button } from '@/components/ui/button';
import { useT } from '@/lib/i18n';
import { cn } from '@/lib/utils';

export const AVATAR_MAX_BYTES = 5 * 1024 * 1024;

/**
 * Pure staging UI: picking or dropping a file (or removing the photo) only reports
 * the intent up via `onSelectFile`/`onRemove` — the actual upload/removal request is
 * deferred until the parent's own "save changes" runs.
 */
export default function AvatarUpload({
  displayUrl,
  username,
  pending,
  disabled,
  error,
  onSelectFile,
  onRemove,
}: {
  displayUrl: string | null;
  username: string;
  pending: boolean;
  disabled?: boolean;
  error: string | null;
  onSelectFile: (file: File) => void;
  onRemove: () => void;
}) {
  const t = useT();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  return (
    <div className="tt-card p-6">
      <h3 className="font-heading text-lg font-semibold">{t('profile.avatar')}</h3>
      <p className="mt-1 mb-4 text-xs text-muted-foreground">{t('profile.avatar.hint')}</p>

      <div
        onDragEnter={(e) => { e.preventDefault(); if (!disabled) setDragging(true); }}
        onDragOver={(e) => e.preventDefault()}
        onDragLeave={(e) => { e.preventDefault(); setDragging(false); }}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          if (disabled) return;
          const file = e.dataTransfer.files?.[0];
          if (file) onSelectFile(file);
        }}
        className={cn(
          'flex flex-col items-center gap-3 rounded-xl border-2 border-dashed p-6 text-center transition-colors sm:flex-row sm:text-left',
          dragging ? 'border-primary bg-primary/5' : 'border-border',
        )}
      >
        <UserAvatar username={username} avatarUrl={displayUrl} size="xl" />

        <div className="min-w-0 flex-1">
          <p className="text-sm text-muted-foreground">{t('profile.avatar.dropHere')}</p>
          {pending && <p className="mt-1 text-xs text-primary">{t('profile.avatar.pending')}</p>}
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <Button type="button" size="sm" disabled={disabled} onClick={() => inputRef.current?.click()}>
              <Upload className="size-4" />
              {t('profile.avatar.browse')}
            </Button>
            {displayUrl && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={disabled}
                onClick={onRemove}
                className="text-xs text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
              >
                <Trash2 className="size-3.5" />
                {t('profile.avatar.remove')}
              </Button>
            )}
          </div>
        </div>
      </div>

      {error && <p className="mt-3 text-sm text-destructive">{error}</p>}

      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        hidden
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onSelectFile(file);
          e.target.value = '';
        }}
      />
    </div>
  );
}
