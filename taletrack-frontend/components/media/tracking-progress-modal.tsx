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
  /** Series only — the furthest episode reached, and episode count per season (index 0 = season 1). */
  season?: number | null;
  episode?: number | null;
  seasonEpisodeCounts?: number[] | null;
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
  const [season, setSeason] = useState(1);
  const [episode, setEpisode] = useState(1);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isSeries = target?.type === 'Series';
  const counts = target?.seasonEpisodeCounts;
  const hasCounts = !!counts && counts.length > 0;
  const episodesInSeason = hasCounts ? (counts![season - 1] ?? 1) : undefined;

  useEffect(() => {
    if (open && target) {
      setProgress(target.progress ?? 0);
      setSeason(target.season ?? 1);
      setEpisode(target.episode ?? 1);
      setError(null);
    }
  }, [open, target]);

  const handleSubmit = async () => {
    if (!target) return;
    setSubmitting(true);
    setError(null);
    try {
      if (isSeries) {
        await trackingService.editSeriesEpisode(target.mediaId, season, episode);
      } else {
        await trackingService.editProgress(target.mediaId, progress);
      }
      onOpenChange(false);
      onSaved?.();
      router.refresh();
    } catch {
      setError(t('trackingModal.saveError'));
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
            />
            <div className="flex min-w-0 flex-1 flex-col gap-2">
              {isSeries ? (
                <>
                  {!hasCounts && (
                    <p className="text-xs text-muted-foreground">{t('trackingModal.seasonUnknown')}</p>
                  )}
                  <div className="flex gap-2">
                    <div className="flex-1">
                      <label
                        htmlFor="tracking-season"
                        className="mb-1 block text-xs font-medium text-muted-foreground"
                      >
                        {t('trackingModal.season')}
                      </label>
                      {hasCounts ? (
                        <select
                          id="tracking-season"
                          value={season}
                          onChange={(e) => {
                            setSeason(Number(e.target.value));
                            setEpisode(1);
                          }}
                          className="tt-input w-full px-2 py-1 text-sm"
                        >
                          {counts!.map((_, i) => (
                            <option key={i} value={i + 1}>
                              {i + 1}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <input
                          id="tracking-season"
                          type="number"
                          min={1}
                          value={season}
                          onChange={(e) => setSeason(Math.max(1, Number(e.target.value) || 1))}
                          className="tt-input w-full px-2 py-1 text-sm"
                        />
                      )}
                    </div>
                    <div className="flex-1">
                      <label
                        htmlFor="tracking-episode"
                        className="mb-1 block text-xs font-medium text-muted-foreground"
                      >
                        {t('trackingModal.episode')}
                      </label>
                      {hasCounts ? (
                        <select
                          id="tracking-episode"
                          value={episode}
                          onChange={(e) => setEpisode(Number(e.target.value))}
                          className="tt-input w-full px-2 py-1 text-sm"
                        >
                          {Array.from({ length: episodesInSeason ?? 1 }, (_, i) => i + 1).map((ep) => (
                            <option key={ep} value={ep}>
                              {ep}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <input
                          id="tracking-episode"
                          type="number"
                          min={1}
                          value={episode}
                          onChange={(e) => setEpisode(Math.max(1, Number(e.target.value) || 1))}
                          className="tt-input w-full px-2 py-1 text-sm"
                        />
                      )}
                    </div>
                  </div>
                </>
              ) : (
                <>
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
                </>
              )}
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
