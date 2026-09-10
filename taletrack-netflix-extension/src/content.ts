// While the user watches (logged in), report title + progress to the backend
// via the service worker. Metadata is resolved once per video; each tick only
// re-reads the live <video> position. The service worker throttles the writes.
// Page-reading logic (what's on this Netflix page) lives in ./netflix-extract.

import type { NetflixMedia, ExtractDataMessage } from './types';
import { NO_TITLE, extractNetflixData, requestNetflixData } from './netflix-extract';

chrome.runtime.onMessage.addListener((message: ExtractDataMessage, _sender, sendResponse) => {
  if (message.action === 'extractData') {
    // Ask the page context for Netflix data
    requestNetflixData().then(() => {
      const data = extractNetflixData();

      if (data) {
        sendResponse({ success: true, data });
      } else {
        sendResponse({ success: false, error: 'No se pudieron extraer los datos' });
      }
    });

    return true; // Keep the channel open
  }

  return true;
});

function getCurrentVideoId(): string | null {
  const match = window.location.href.match(/\/watch\/(\d+)/);
  return match && match[1] ? match[1] : null;
}

const AUTO_POLL_MS = 15000;
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

async function resolveMediaFor(videoId: string): Promise<void> {
  if (trackedVideoId === videoId && trackedMedia) return;
  if (resolvingVideoId === videoId) return; // already retrying for this video
  resolvingVideoId = videoId;

  try {
    await requestNetflixData();
    let media = extractNetflixData();

    // The on-screen title bar fades out a few seconds into playback — if we
    // land here after it's already gone, retry briefly. It's reliably shown
    // right when a video starts loading (see the 'loadedmetadata' listener
    // in startAutoTracker, which calls us at that exact moment).
    for (const delay of [500, 1000, 2000, 4000]) {
      if (media?.title !== NO_TITLE) break;
      if (getCurrentVideoId() !== videoId) return; // moved on to another video
      await new Promise((resolve) => setTimeout(resolve, delay));
      if (getCurrentVideoId() !== videoId) return;
      await requestNetflixData();
      media = extractNetflixData();
    }

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

  // The title bar is reliably on screen right when a new video starts
  // loading — react immediately instead of waiting for the next poll tick
  // (up to AUTO_POLL_MS later, by which point it has usually faded out).
  document.addEventListener(
    'loadedmetadata',
    (e) => {
      if ((e.target as HTMLElement)?.tagName === 'VIDEO') {
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
