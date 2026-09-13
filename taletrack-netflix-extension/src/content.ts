// While the user watches (logged in), report title + progress to the backend
// via the service worker. Metadata is resolved once per video; each tick only
// re-reads the live <video> position. The service worker throttles the writes.
// Page-reading logic (what's on this Netflix page) lives in ./netflix-extract.

import type { NetflixMedia, ExtractDataMessage } from './types';
import { NO_TITLE, extractNetflixData, requestNetflixData } from './netflix-extract';
import { AUTO_POLL_MS } from './config';

chrome.runtime.onMessage.addListener((message: ExtractDataMessage, _sender, sendResponse) => {
  if (message.action === 'extractData') {
    (async () => {
      // Share the auto-tracker's resolution (retries + per-video cache) on a
      // /watch/ page, so this reports exactly what real tracking would see.
      const videoId = getCurrentVideoId();
      if (videoId) {
        await resolveMediaFor(videoId);
        if (trackedVideoId === videoId && trackedMedia) {
          sendResponse({ success: true, data: trackedMedia });
          return;
        }
      }

      // Not on a /watch/ page (e.g. a title/browse page) — one-shot read.
      await requestNetflixData();
      const data = extractNetflixData();
      if (data) {
        sendResponse({ success: true, data });
      } else {
        sendResponse({ success: false, error: 'No se pudieron extraer los datos' });
      }
    })();

    return true; // Keep the channel open
  }

  return true;
});

function getCurrentVideoId(): string | null {
  const match = window.location.href.match(/\/watch\/(\d+)/);
  return match && match[1] ? match[1] : null;
}

const AUTH_BACKOFF_MS = 5 * 60 * 1000;

let trackedVideoId: string | null = null;
let trackedMedia: NetflixMedia | null = null;
let backoffUntil = 0;

function livePlayback(): { percent: number; runtimeSeconds: number } | null {
  const video = document.querySelector('video') as HTMLVideoElement | null;
  if (!video || !Number.isFinite(video.duration) || video.duration <= 0) return null;
  if (!Number.isFinite(video.currentTime)) return null;
  return {
    percent: Math.max(0, Math.min(100, Math.round((video.currentTime / video.duration) * 100))),
    runtimeSeconds: Math.round(video.duration),
  };
}

let resolvingVideoId: string | null = null;

// Simulates pointer activity over the player, which is what keeps Netflix's
// title-bar overlay (extractTitle()'s DOM source) on screen.
function nudgeNetflixControls(): void {
  const target: Element = document.querySelector('video') ?? document.body;
  const rect = target.getBoundingClientRect();
  const evt = new MouseEvent('mousemove', {
    bubbles: true,
    cancelable: true,
    clientX: rect.left + rect.width / 2,
    clientY: rect.top + rect.height / 2,
  });
  target.dispatchEvent(evt);
}

async function resolveMediaFor(videoId: string): Promise<void> {
  if (trackedVideoId === videoId && trackedMedia) return;
  if (resolvingVideoId === videoId) return; // already retrying for this video
  resolvingVideoId = videoId;

  try {
    console.log('[TaleTrack] resolveMediaFor: resolving videoId', videoId);
    nudgeNetflixControls();
    await requestNetflixData();
    let media = extractNetflixData();
    console.log('[TaleTrack] resolveMediaFor: first attempt ->', media?.title);

    // The on-screen title bar is only shown briefly, so poll for it: check
    // every second for up to ten seconds, which gives a slow connection room
    // to actually render it before we give up.
    for (const delay of Array(10).fill(1000)) {
      if (media?.title !== NO_TITLE) break;
      if (getCurrentVideoId() !== videoId) return; // moved on to another video
      console.log(`[TaleTrack] resolveMediaFor: no title yet, retrying in ${delay}ms…`);
      await new Promise((resolve) => setTimeout(resolve, delay));
      if (getCurrentVideoId() !== videoId) return;
      nudgeNetflixControls();
      await requestNetflixData();
      media = extractNetflixData();
      console.log(`[TaleTrack] resolveMediaFor: retry (${delay}ms) ->`, media?.title);
    }

    if (media?.title === NO_TITLE) {
      // Don't mark this video as resolved with a placeholder title, or it'd get
      // tracked under the literal string "Título no encontrado". Leave it unset
      // so the next tick retries from scratch.
      console.log('[TaleTrack] resolveMediaFor: no real title after retries, not tracking', videoId, 'this cycle');
      return;
    }

    console.log('[TaleTrack] resolveMediaFor: settled on ->', media?.title, 'for videoId', videoId);
    trackedVideoId = videoId;
    trackedMedia = media;
  } finally {
    resolvingVideoId = null;
  }
}

function reportProgress(flush: boolean): void {
  if (Date.now() < backoffUntil) return;
  const videoId = getCurrentVideoId();
  if (!videoId || !trackedMedia || trackedVideoId !== videoId) return;

  const playback = livePlayback();
  if (!playback) return;

  const media: NetflixMedia = {
    ...trackedMedia,
    progressPercent: playback.percent,
    runtimeSeconds: trackedMedia.runtimeSeconds ?? playback.runtimeSeconds,
  };

  chrome.runtime.sendMessage(
    { type: 'TRACK_PROGRESS', payload: { videoId, media, progressPercent: playback.percent, flush } },
    (res?: { ok: boolean; reason?: string }) => {
      if (chrome.runtime.lastError) return; // service worker asleep / popup closed
      if (res && !res.ok && res.reason === 'unauthenticated') {
        backoffUntil = Date.now() + AUTH_BACKOFF_MS;
      }
    },
  );
}

async function autoTick(): Promise<void> {
  const videoId = getCurrentVideoId();
  console.log('[TaleTrack] autoTick @', new Date().toLocaleTimeString(), 'videoId =', videoId);
  if (!videoId) return;

  // Switched episodes / titles: send a final reading for the previous one.
  if (trackedVideoId && trackedVideoId !== videoId) {
    reportProgress(true);
    trackedMedia = null;
  }

  await resolveMediaFor(videoId);
  reportProgress(false);
}

function startAutoTracker(): void {
  setInterval(() => {
    autoTick().catch((err) => console.error('TaleTrack auto-tick failed:', err));
  }, AUTO_POLL_MS);

  // This script loads at document_idle, so on a fresh /watch/ page the <video>
  // may have already fired 'loadedmetadata' before the listener below attaches.
  // Check once immediately too, or a late-caught video misses the title bar.
  console.log('[TaleTrack] startAutoTracker: initial check on script load');
  autoTick().catch((err) => console.error('TaleTrack auto-tick failed:', err));

  document.addEventListener(
    'loadedmetadata',
    (e) => {
      if ((e.target as HTMLElement)?.tagName === 'VIDEO') {
        console.log('[TaleTrack] loadedmetadata fired');
        autoTick().catch((err) => console.error('TaleTrack auto-tick failed:', err));
      }
    },
    true,
  );

  const flush = () => reportProgress(true);
  window.addEventListener('pagehide', flush);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') flush();
  });
  document.addEventListener('ended', (e) => {
    if ((e.target as HTMLElement)?.tagName === 'VIDEO') flush();
  }, true);
}

startAutoTracker();
