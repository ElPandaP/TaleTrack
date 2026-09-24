// While the user watches (logged in), report title + progress to the backend
// via the service worker. Metadata is resolved once per video; each tick only
// re-reads the live <video> position. The service worker throttles the writes.
// Page-reading logic (what's on this Netflix page) lives in ./netflix-extract.

import type { NetflixMedia, TrackResult } from './types';
import {
  extractNetflixData,
  requestNetflixData,
  getCreditsOffsetSeconds,
  getWatchVideoId,
  CREDITS_MARGIN_SECONDS,
} from './netflix-extract';
import { AUTO_POLL_MS, debug } from './config';

const AUTH_BACKOFF_MS = 5 * 60 * 1000;

// The on-screen title bar is only shown briefly, so resolving a title retries
// once a second, which gives a slow connection room to render it.
const TITLE_RETRIES = 10;
const TITLE_RETRY_MS = 1000;

/** One reading of the tracked video's progress, kept so it can still be sent after the player is gone. */
interface Reading {
  videoId: string;
  media: NetflixMedia;
  percent: number;
}

let trackedVideoId: string | null = null;
let trackedMedia: NetflixMedia | null = null;
let trackedCreditsOffsetSeconds: number | null = null;
let lastReading: Reading | null = null;
let backoffUntil = 0;
let resolvingVideoId: string | null = null;

function livePlayback(creditsOffsetSeconds: number | null): { percent: number; runtimeSeconds: number } | null {
  const video = document.querySelector('video');
  if (!video || !Number.isFinite(video.duration) || video.duration <= 0) return null;
  if (!Number.isFinite(video.currentTime)) return null;

  let percent = Math.max(0, Math.min(100, Math.round((video.currentTime / video.duration) * 100)));

  // Once within reach of the credits — or already past them — count it as
  // done, instead of tracking stalling just under 100% (Netflix often cuts
  // to the next episode before the <video> itself reaches its duration).
  if (creditsOffsetSeconds !== null && video.currentTime >= creditsOffsetSeconds - CREDITS_MARGIN_SECONDS) {
    percent = 100;
  }

  return {
    percent,
    runtimeSeconds: Math.round(video.duration),
  };
}

// TODO: revisar si es necesario al hacer testing (puede que ya no haga falta y se pueda quitar).
// Si se quita, hay que tocar también:
//  - la llamada a nudgeNetflixControls() en resolveMediaFor (este fichero);
//  - el comentario de extractTitle() en netflix-extract.ts sobre la barra de título que se desvanece;
//  - TFG/final/Ch5.md, apartado 5.1.3 (Netflix): la frase que dice que la extensión "simula un
//    movimiento del ratón" (y el argumento de la automatización que la rodea);
//  - README de la extensión, si menciona el mantenimiento de la barra de título.
// Simulates pointer activity over the player, which is what keeps Netflix's
// title-bar overlay (extractTitle()'s DOM source) on screen. It has a visible
// side effect: the player controls pop up each time a title is being resolved.
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
    debug('resolving media for video', videoId);
    let media: NetflixMedia | null = null;

    for (let attempt = 0; attempt <= TITLE_RETRIES; attempt++) {
      if (attempt > 0) await new Promise((resolve) => setTimeout(resolve, TITLE_RETRY_MS));
      if (getWatchVideoId() !== videoId) return; // moved on to another video

      nudgeNetflixControls();
      await requestNetflixData();
      media = extractNetflixData();
      if (media) break;
      debug(`no title yet (attempt ${attempt + 1} of ${TITLE_RETRIES + 1})`);
    }

    // Don't mark the video as resolved without a real title; leave it unset so
    // the next tick retries from scratch.
    if (!media || getWatchVideoId() !== videoId) {
      debug('no title for video', videoId, 'this cycle, not tracking');
      return;
    }

    debug('resolved', media.title, 'for video', videoId);
    trackedVideoId = videoId;
    trackedMedia = media;
    trackedCreditsOffsetSeconds = getCreditsOffsetSeconds();
  } finally {
    resolvingVideoId = null;
  }
}

// Netflix's Falcor cache may not have the credits offset yet when the title
// first resolves, so keep asking until it does.
async function refreshCreditsOffset(videoId: string): Promise<void> {
  if (trackedVideoId !== videoId || trackedCreditsOffsetSeconds !== null) return;

  await requestNetflixData();
  if (trackedVideoId === videoId && getWatchVideoId() === videoId) {
    trackedCreditsOffsetSeconds = getCreditsOffsetSeconds();
  }
}

function send(reading: Reading, flush: boolean): void {
  if (Date.now() < backoffUntil) return;

  try {
    chrome.runtime.sendMessage(
      {
        type: 'TRACK_PROGRESS',
        payload: { videoId: reading.videoId, media: reading.media, progressPercent: reading.percent, flush },
      },
      (res?: TrackResult) => {
        if (chrome.runtime.lastError) return; // service worker asleep / popup closed
        if (res && !res.ok && res.reason === 'unauthenticated') {
          backoffUntil = Date.now() + AUTH_BACKOFF_MS;
        }
      },
    );
  } catch {
    // The extension was reloaded or updated under this page: nothing left to report to.
  }
}

/** Reads the live <video> position of the tracked video and reports it. */
function reportProgress(flush: boolean): void {
  const videoId = getWatchVideoId();
  if (!videoId || !trackedMedia || trackedVideoId !== videoId) return;

  const playback = livePlayback(trackedCreditsOffsetSeconds);
  if (!playback) return;

  lastReading = {
    videoId,
    media: { ...trackedMedia, runtimeSeconds: trackedMedia.runtimeSeconds ?? playback.runtimeSeconds },
    percent: playback.percent,
  };
  send(lastReading, flush);
}

/**
 * The tracked video is no longer on screen (another title, or the player is closed). The <video>
 * can't be read for it any more, so its last reading is the final word on it.
 */
function finishTrackedVideo(): void {
  if (lastReading) send(lastReading, true);
  lastReading = null;
  trackedVideoId = null;
  trackedMedia = null;
  trackedCreditsOffsetSeconds = null;
}

async function autoTick(): Promise<void> {
  const videoId = getWatchVideoId();
  debug('autoTick', videoId);

  // Switched episodes / titles, or left the player: send a final reading for the previous one.
  if (trackedVideoId && trackedVideoId !== videoId) finishTrackedVideo();
  if (!videoId) return;

  await resolveMediaFor(videoId);
  await refreshCreditsOffset(videoId);
  reportProgress(false);
}

function startAutoTracker(): void {
  const tick = () => autoTick().catch((err) => console.error('TaleTrack auto-tick failed:', err));

  setInterval(tick, AUTO_POLL_MS);

  // This script loads at document_idle, so on a fresh /watch/ page the <video>
  // may have already fired 'loadedmetadata' before the listener below attaches.
  // Check once immediately too, or a late-caught video misses the title bar.
  tick();

  document.addEventListener(
    'loadedmetadata',
    (e) => {
      if ((e.target as HTMLElement)?.tagName === 'VIDEO') tick();
    },
    true,
  );

  // Moments with no next tick to count on: the tab is closing or hidden, or playback ended.
  const flush = () => reportProgress(true);
  window.addEventListener('pagehide', flush);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') flush();
  });
  document.addEventListener(
    'ended',
    (e) => {
      if ((e.target as HTMLElement)?.tagName === 'VIDEO') flush();
    },
    true,
  );
}

startAutoTracker();
