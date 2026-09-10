'use client';

import { useEffect, useRef, useState } from 'react';
import { Upload, Loader2, Trash2 } from 'lucide-react';
import { UserAvatar } from '@/components/media/user-avatar';
import { userService } from '@/lib/api/services';
import { useT } from '@/lib/i18n';
import { cn } from '@/lib/utils';

const MAX_BYTES = 5 * 1024 * 1024;

export default function AvatarUpload({
  currentUrl,
  username,
  onChange,
}: {
  currentUrl: string;
  username: string;
  onChange: (url: string | null) => void;
}) {
  const t = useT();
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  useEffect(() => () => {
    if (preview) URL.revokeObjectURL(preview);
  }, [preview]);

  const handleFile = async (file: File) => {
    setError(null);
    if (!file.type.startsWith('image/')) {
      setError(t('profile.avatar.invalidType'));
      return;
    }
    if (file.size > MAX_BYTES) {
      setError(t('profile.avatar.tooLarge'));
      return;
    }

    const localPreview = URL.createObjectURL(file);
    setPreview(localPreview);
    setBusy(true);
    try {
      const res = await userService.uploadAvatar(file);
      onChange(res.avatarUrl);
    } catch {
      setError(t('profile.avatar.uploadError'));
    } finally {
      setBusy(false);
      setPreview(null); // fall back to the saved URL
      URL.revokeObjectURL(localPreview);
    }
  };

  const handleRemove = async () => {
    setError(null);
    setBusy(true);
    try {
      await userService.removeAvatar();
      onChange(null);
    } catch {
      setError(t('profile.avatar.uploadError'));
    } finally {
      setBusy(false);
    }
  };

  const shown = preview ?? (currentUrl || null);

  return (
    <div className="tt-card p-6">
      <h3 className="font-heading text-lg font-semibold">{t('profile.avatar')}</h3>
      <p className="mt-1 mb-4 text-xs text-muted-foreground">{t('profile.avatar.hint')}</p>

      <div
        onDragEnter={(e) => { e.preventDefault(); setDragging(true); }}
        onDragOver={(e) => e.preventDefault()}
        onDragLeave={(e) => { e.preventDefault(); setDragging(false); }}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          const file = e.dataTransfer.files?.[0];
          if (file) void handleFile(file);
        }}
        className={cn(
          'flex flex-col items-center gap-3 rounded-xl border-2 border-dashed p-6 text-center transition-colors sm:flex-row sm:text-left',
          dragging ? 'border-primary bg-primary/5' : 'border-border',
        )}
      >
        <div className="relative">
          <UserAvatar username={username} avatarUrl={shown} size="xl" />
          {busy && (
            <span className="absolute inset-0 flex items-center justify-center rounded-2xl bg-background/60">
              <Loader2 className="size-5 animate-spin text-primary" />
            </span>
          )}
        </div>

        <div className="min-w-0 flex-1">
          <p className="text-sm text-muted-foreground">{t('profile.avatar.dropHere')}</p>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <button
              type="button"
              disabled={busy}
              onClick={() => inputRef.current?.click()}
              className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90 disabled:opacity-50"
            >
              <Upload className="size-4" />
              {t('profile.avatar.browse')}
            </button>
            {currentUrl && (
              <button
                type="button"
                disabled={busy}
                onClick={handleRemove}
                className="inline-flex items-center gap-1.5 text-xs text-muted-foreground transition-colors hover:text-destructive disabled:opacity-50"
              >
                <Trash2 className="size-3.5" />
                {t('profile.avatar.remove')}
              </button>
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
          if (file) void handleFile(file);
          e.target.value = '';
        }}
      />
    </div>
  );
}
