'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Cover } from '@/components/media/cover';
import { trackingService } from '@/lib/api/services';
import { useT } from '@/lib/i18n';
import type { LibraryType } from '@/lib/types';

export interface TrackingProgressTarget {
  mediaId: number;
  title: string;
  type: LibraryType;
  posterUrl?: string | null;
  progress?: number | null;
}

export function TrackingProgressModal({
  target,
  open,
  onOpenChange,
  onSaved,
}: {
  target: TrackingProgressTarget | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: () => void;
}) {
  const router = useRouter();
  const t = useT();
  const [progress, setProgress] = useState(0);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open && target) {
      setProgress(target.progress ?? 0);
      setError(null);
    }
  }, [open, target]);

  const handleSubmit = async () => {
    if (!target) return;
    setSubmitting(true);
    setError(null);
    try {
      await trackingService.editProgress(target.mediaId, progress);
      onOpenChange(false);
      onSaved?.();
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : t('trackingModal.saveError'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{t('trackingModal.title')}</DialogTitle>
          <DialogDescription>
            {target
              ? t('trackingModal.subtitle', { type: t(`type.${target.type}`), title: target.title })
              : ''}
          </DialogDescription>
        </DialogHeader>

        {target && (
          <div className="flex gap-4">
            <Cover
              title={target.title}
              type={target.type}
              posterUrl={target.posterUrl}
              className="w-20 shrink-0 self-start"
              sizes="80px"
            />
            <div className="flex min-w-0 flex-1 flex-col gap-2">
              <div className="flex items-center justify-between">
                <label
                  htmlFor="tracking-progress"
                  className="text-xs font-medium text-muted-foreground"
                >
                  {t('trackingModal.progress')}
                </label>
                <span className="text-sm font-medium text-foreground">{progress}%</span>
              </div>
              <input
                id="tracking-progress"
                type="range"
                min={0}
                max={100}
                value={progress}
                onChange={(e) => setProgress(Number(e.target.value))}
                className="w-full accent-primary"
              />
              <input
                type="number"
                min={0}
                max={100}
                value={progress}
                onChange={(e) => setProgress(Math.min(100, Math.max(0, Number(e.target.value) || 0)))}
                className="tt-input w-20 px-2 py-1 text-sm"
              />
            </div>
          </div>
        )}

        {error && (
          <p className="rounded-lg bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>
        )}

        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('trackingModal.cancel')}
          </Button>
          <Button onClick={handleSubmit} disabled={submitting}>
            {submitting ? t('trackingModal.saving') : t('trackingModal.save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
