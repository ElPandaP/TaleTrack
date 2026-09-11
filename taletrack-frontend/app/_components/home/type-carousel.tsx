'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import useEmblaCarousel from 'embla-carousel-react';
import { ChevronLeft, ChevronRight, ArrowRight } from 'lucide-react';
import { Cover } from '@/components/media/cover';
import { StarRating, toStars } from '@/components/media/star-rating';
import { useT } from '@/lib/i18n';
import type { LibraryItem, LibraryType } from '@/lib/types';

export function TypeCarousel({
  type,
  items,
  total,
}: {
  type: LibraryType;
  items: LibraryItem[];
  total?: number;
}) {
  const t = useT();
  const plural = t(`typePlural.${type}`);
  const count = total ?? items.length;
  const [emblaRef, emblaApi] = useEmblaCarousel({
    dragFree: true,
    align: 'start',
    containScroll: 'trimSnaps',
  });
  const [canPrev, setCanPrev] = useState(false);
  const [canNext, setCanNext] = useState(false);
  const downX = useRef(0);

  const sync = useCallback(() => {
    if (!emblaApi) return;
    setCanPrev(emblaApi.canScrollPrev());
    setCanNext(emblaApi.canScrollNext());
  }, [emblaApi]);

  useEffect(() => {
    if (!emblaApi) return;
    const raf = requestAnimationFrame(sync); // initial button state, off the effect body
    emblaApi.on('select', sync).on('reInit', sync);
    return () => {
      cancelAnimationFrame(raf);
      emblaApi.off('select', sync).off('reInit', sync);
    };
  }, [emblaApi, sync]);

  // A drag that ends over a slide shouldn't navigate.
  const onPointerDown = (e: React.PointerEvent) => {
    downX.current = e.clientX;
  };
  const onClickCapture = (e: React.MouseEvent) => {
    if (Math.abs(e.clientX - downX.current) > 8) {
      e.preventDefault();
      e.stopPropagation();
    }
  };

  return (
    <section aria-labelledby={`carousel-${type}`}>
      <div className="mb-3 flex items-baseline justify-between gap-2">
        <h2 id={`carousel-${type}`} className="font-heading text-xl font-semibold">
          {plural}
        </h2>
        <div className="flex items-center gap-2">
          <Link
            href={`/library?type=${type}`}
            className="text-xs text-muted-foreground transition-colors hover:text-foreground"
          >
            {t('home.carousel.seeAllCount', { count })}
          </Link>
          <div className="flex gap-1">
            <button
              type="button"
              onClick={() => emblaApi?.scrollPrev()}
              disabled={!canPrev}
              aria-label={t('home.carousel.prev', { items: plural })}
              className="flex size-7 items-center justify-center rounded-full border border-border text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground disabled:opacity-30 disabled:hover:bg-transparent"
            >
              <ChevronLeft aria-hidden="true" className="size-4" />
            </button>
            <button
              type="button"
              onClick={() => emblaApi?.scrollNext()}
              disabled={!canNext}
              aria-label={t('home.carousel.next', { items: plural })}
              className="flex size-7 items-center justify-center rounded-full border border-border text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground disabled:opacity-30 disabled:hover:bg-transparent"
            >
              <ChevronRight aria-hidden="true" className="size-4" />
            </button>
          </div>
        </div>
      </div>

      {items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-border px-4 py-6 text-center text-sm text-muted-foreground">
          {t('home.carousel.empty', { items: plural.toLowerCase() })}
        </p>
      ) : (
        <div
          className="cursor-grab overflow-hidden active:cursor-grabbing"
          ref={emblaRef}
          onPointerDownCapture={onPointerDown}
          onClickCapture={onClickCapture}
        >
          <div className="flex gap-3">
            {items.map((item) => (
              <figure key={item.mediaId} className="shrink-0 grow-0 basis-[7.5rem]">
                <Link href={`/media/${item.mediaId}`} draggable={false} className="group block">
                  <Cover
                    title={item.title}
                    type={item.type}
                    posterUrl={item.posterUrl}
                    className="transition group-hover:shadow-md group-hover:brightness-[1.03]"
                  />
                  <figcaption className="mt-1.5">
                    <p className="line-clamp-2 text-xs leading-tight text-foreground group-hover:text-primary">
                      {item.title}
                    </p>
                    {item.myRating != null && (
                      <StarRating stars={toStars(item.myRating)} className="mt-0.5" />
                    )}
                  </figcaption>
                </Link>
              </figure>
            ))}

            {count > items.length && (
              <figure className="shrink-0 grow-0 basis-[7.5rem]">
                <Link
                  href={`/library?type=${type}`}
                  draggable={false}
                  className="group flex aspect-2/3 flex-col items-center justify-center gap-1.5 rounded-xl border-2 border-dashed border-border text-muted-foreground transition-colors hover:border-primary/40 hover:bg-primary/5 hover:text-primary"
                >
                  <ArrowRight aria-hidden="true" className="size-5" />
                  <span className="text-xs font-medium">{t('home.carousel.seeAll')}</span>
                  <span className="text-[11px] opacity-70">{count}</span>
                </Link>
              </figure>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
