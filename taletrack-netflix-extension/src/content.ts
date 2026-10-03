/**
 * Content script that runs on every Netflix page and drives the auto-tracking.
 *
 * It decides when to look at the page and what to report: while a /watch/ page is open it polls
 * every {@link AUTO_POLL_MS}, reacts at once to URL changes and to new videos loading, and sends
 * the playback progress to the background service worker, which throttles and forwards it to the
 * backend. What is on the page (title, type, season and episode) is worked out by the
 * `netflix-extract` module.
 *
 * The media is resolved once per video and then kept; each tick only re-reads the position of
 * the `<video>` element. Once playback reaches the credits the progress counts as 100%.
 *
 * All of this only runs while the extension is signed in: the script asks the service worker
 * when it loads, and the extension tells it whenever the session starts or ends.
 *
 * @module
 */

import type { InternalMessage, NetflixMedia, SessionChangedMessage, SessionStatus, TrackResult } from './types';
import {
  extractNetflixData,
  requestNetflixData,
  getCreditsOffsetSeconds,
  getWatchVideoId,
  CREDITS_MARGIN_SECONDS,
} from './netflix-extract';
import { AUTO_POLL_MS, debug } from './config';

/**
 * Extra attempts to find a title for a new video before waiting for the next tick. The on-screen
 * title bar is only shown briefly, so retrying once a second gives a slow connection room to
 * render it.
 */
const TITLE_RETRIES = 10;
/** Delay between title attempts, in milliseconds. */
const TITLE_RETRY_MS = 1000;

/** How often to check for another /watch/ id where the Navigation API is missing. */
const URL_CHECK_MS = 500;

/** One reading of the tracked video's progress, kept so it can still be sent after the player is gone. */
interface Reading {
  videoId: string;
  media: NetflixMedia;
  percent: number;
}

/** Video id whose media has been resolved and is being tracked. */
let trackedVideoId: string | null = null;
/** Media resolved for `trackedVideoId`. */
let trackedMedia: NetflixMedia | null = null;
/** Where the tracked video's credits start, in seconds, once known. */
let trackedCreditsOffsetSeconds: number | null = null;
/** Last progress reading of the tracked video. */
let lastReading: Reading | null = null;
/** Video id whose title is being resolved right now, so overlapping ticks don't start a second attempt. */
let resolvingVideoId: string | null = null;

/**
 * Reads the playback position of the page's `<video>` element.
 *
 * @param creditsOffsetSeconds - Where the credits start, if known; from there on progress is 100%.
 * @returns Progress (0 to 100) and runtime in seconds, or null when there is no usable video yet.
 */
function livePlayback(creditsOffsetSeconds: number | null): { percent: number; runtimeSeconds: number } | null {
  const video = document.querySelector('video');
  if (!video) {
    debug('no <video> element on the page');
    return null;
  }
  if (!Number.isFinite(video.duration) || video.duration <= 0 || !Number.isFinite(video.currentTime)) {
    debug('<video> has no usable duration/position yet:', video.duration, video.currentTime);
    return null;
  }

  let percent = Math.max(0, Math.min(100, Math.round((video.currentTime / video.duration) * 100)));

  // Within reach of the credits, or past them, counts as done: Netflix often cuts to the next
  // episode before the <video> itself reaches its duration, so the progress would otherwise
  // stall just under 100%.
  if (creditsOffsetSeconds !== null && video.currentTime >= creditsOffsetSeconds - CREDITS_MARGIN_SECONDS) {
    debug('within reach of the credits, counting as 100%');
    percent = 100;
  }

  return {
    percent,
    runtimeSeconds: Math.round(video.duration),
  };
}

/**
 * Works out the media for a video and makes it the tracked one.
 *
 * Asks the page for Netflix's data and extracts the media, retrying up to `TITLE_RETRIES` times
 * while no source has a title yet. It stops early if the user moves to another video, and does
 * nothing when the video is already resolved or being resolved.
 *
 * @param videoId - The /watch/ id to resolve.
 */
async function resolveMediaFor(videoId: string): Promise<void> {
  if (trackedVideoId === videoId && trackedMedia) return;
  if (resolvingVideoId === videoId) {
    debug('still resolving video', videoId, 'from a previous tick');
    return;
  }
  resolvingVideoId = videoId;

  try {
    debug('resolving media for video', videoId);
    let media: NetflixMedia | null = null;

    for (let attempt = 0; attempt <= TITLE_RETRIES; attempt++) {
      if (attempt > 0) await new Promise((resolve) => setTimeout(resolve, TITLE_RETRY_MS));
      if (getWatchVideoId() !== videoId) {
        debug('left video', videoId, 'while resolving its title');
        return;
      }

      await requestNetflixData();
      media = extractNetflixData();
      if (media) break;
      debug(`no title yet (attempt ${attempt + 1} of ${TITLE_RETRIES + 1})`);
    }

    // Without a real title the video stays unresolved, so the next tick retries from scratch.
    if (!media || getWatchVideoId() !== videoId) {
      debug('no title for video', videoId, 'this cycle, not tracking');
      return;
    }

    trackedVideoId = videoId;
    trackedMedia = media;
    trackedCreditsOffsetSeconds = getCreditsOffsetSeconds();
    debug('resolved video', videoId, '->', media, 'credits at', trackedCreditsOffsetSeconds, 's');
  } finally {
    resolvingVideoId = null;
  }
}

/**
 * Asks again for the tracked video's credits offset while it is still unknown. Netflix's data may
 * not include it yet when the title first resolves.
 *
 * @param videoId - The video on screen.
 */
async function refreshCreditsOffset(videoId: string): Promise<void> {
  if (trackedVideoId !== videoId || trackedCreditsOffsetSeconds !== null) return;

  await requestNetflixData();
  if (trackedVideoId === videoId && getWatchVideoId() === videoId) {
    trackedCreditsOffsetSeconds = getCreditsOffsetSeconds();
    debug('credits offset for video', videoId, ':', trackedCreditsOffsetSeconds ?? 'still unknown');
  }
}

/**
 * Sends a reading to the background service worker as a `TRACK_PROGRESS` message.
 *
 * A "no session" answer stops the tracker until the extension signs in again.
 *
 * @param reading - The progress to report.
 * @param flush - Asks the service worker to skip its throttle.
 */
function send(reading: Reading, flush: boolean): void {
  debug(`sending ${reading.percent}% of "${reading.media.title}"${flush ? ' (flush)' : ''} to the service worker`);
  try {
    chrome.runtime.sendMessage(
      {
        type: 'TRACK_PROGRESS',
        payload: { videoId: reading.videoId, media: reading.media, progressPercent: reading.percent, flush },
      },
      (res?: TrackResult) => {
        if (chrome.runtime.lastError) {
          debug('service worker did not answer:', chrome.runtime.lastError.message);
          return;
        }
        debug('service worker answered:', res);
        if (res && !res.ok && res.reason === 'unauthenticated') {
          debug('not signed in to the extension: stopping the tracker');
          setTracking(false);
        }
      },
    );
  } catch (err) {
    // The extension was reloaded or updated under this page: nothing left to report to.
    debug('could not reach the extension (reloaded? refresh the Netflix tab):', err);
  }
}

/**
 * Reads the live `<video>` position of the tracked video, keeps it as the last reading and
 * reports it.
 *
 * @param flush - Asks the service worker to skip its throttle.
 */
function reportProgress(flush: boolean): void {
  const videoId = getWatchVideoId();
  if (!videoId || !trackedMedia || trackedVideoId !== videoId) {
    debug('nothing to report: video', videoId, 'has no resolved media (tracking', trackedVideoId, ')');
    return;
  }

  const playback = livePlayback(trackedCreditsOffsetSeconds);
  if (!playback) return;
  debug(`progress of video ${videoId}: ${playback.percent}% of ${playback.runtimeSeconds}s`);

  lastReading = {
    videoId,
    media: { ...trackedMedia, runtimeSeconds: trackedMedia.runtimeSeconds ?? playback.runtimeSeconds },
    percent: playback.percent,
  };
  send(lastReading, flush);
}

/**
 * Closes the tracked video once it is no longer on screen (another title, or the player is closed).
 * The `<video>` can't be read for it any more, so its last reading is sent as the final word.
 */
function finishTrackedVideo(): void {
  debug('left video', trackedVideoId, ', sending its last reading:', lastReading?.percent ?? 'none');
  if (lastReading) send(lastReading, true);
  lastReading = null;
  trackedVideoId = null;
  trackedMedia = null;
  trackedCreditsOffsetSeconds = null;
}

/**
 * One tracking step: closes the previous video if the user moved on, then resolves the current
 * video, completes its credits offset and reports its progress.
 */
async function autoTick(): Promise<void> {
  const videoId = getWatchVideoId();
  debug('tick on', window.location.pathname, '-> watch id', videoId);

  if (trackedVideoId && trackedVideoId !== videoId) finishTrackedVideo();
  if (!videoId) return;

  await resolveMediaFor(videoId);
  await refreshCreditsOffset(videoId);
  reportProgress(false);
}

/**
 * Sets up the auto-tracker: the poll on /watch/ pages, the URL and video listeners, and the
 * forced reports when the tab is hidden or closed or playback ends.
 *
 * @returns A function that removes all of it again.
 */
function startAutoTracker(): () => void {
  debug('starting the tracker on', window.location.href);
  const listeners = new AbortController();
  const { signal } = listeners;
  let urlTimer: ReturnType<typeof setInterval> | null = null;
  const tick = () => autoTick().catch((err) => console.error('TaleTrack auto-tick failed:', err));

  // Progress only moves while a video is open, so the poll runs only on /watch/ pages.
  let pollTimer: ReturnType<typeof setInterval> | null = null;
  const syncPolling = () => {
    const watching = getWatchVideoId() !== null;
    if (watching && pollTimer === null) {
      pollTimer = setInterval(tick, AUTO_POLL_MS);
    } else if (!watching && pollTimer !== null) {
      clearInterval(pollTimer);
      pollTimer = null;
    }
  };

  // This script loads at document_idle, so on a fresh /watch/ page the <video>
  // may have already fired 'loadedmetadata' before the listener below attaches.
  // Check once immediately too, or a late-caught video misses the title bar.
  if (getWatchVideoId()) tick();
  syncPolling();

  document.addEventListener(
    'loadedmetadata',
    (e) => {
      if ((e.target as HTMLElement)?.tagName === 'VIDEO') tick();
    },
    { capture: true, signal },
  );

  // Netflix is a single-page app: going from /browse to /watch/<id> (or to the next episode)
  // doesn't reload this script. Watching the URL lets a new video start resolving right away,
  // while its title bar is still on screen, without waiting for the next poll.
  let lastWatchId = getWatchVideoId();
  const onUrlChange = () => {
    const watchId = getWatchVideoId();
    if (watchId === lastWatchId) return;
    lastWatchId = watchId;
    debug('URL changed to', window.location.pathname);
    tick(); // also sends the final reading when leaving a video
    syncPolling();
  };
  // The Navigation API reports every URL change, pushState included; browsers without it
  // fall back to checking the URL on a short interval.
  const nav = (window as Window & { navigation?: EventTarget }).navigation;
  if (nav) nav.addEventListener('currententrychange', onUrlChange, { signal });
  else urlTimer = setInterval(onUrlChange, URL_CHECK_MS);

  // Moments with no next tick to count on: the tab is closing or hidden, or playback ended.
  const flush = () => reportProgress(true);
  window.addEventListener('pagehide', flush, { signal });
  document.addEventListener(
    'visibilitychange',
    () => {
      if (document.visibilityState === 'hidden') flush();
    },
    { signal },
  );
  document.addEventListener(
    'ended',
    (e) => {
      if ((e.target as HTMLElement)?.tagName === 'VIDEO') flush();
    },
    { capture: true, signal },
  );

  return () => {
    debug('stopping the tracker');
    listeners.abort();
    if (pollTimer !== null) clearInterval(pollTimer);
    if (urlTimer !== null) clearInterval(urlTimer);
    trackedVideoId = null;
    trackedMedia = null;
    trackedCreditsOffsetSeconds = null;
    lastReading = null;
  };
}

/** Stops the running tracker, or null while signed out. */
let stopTracker: (() => void) | null = null;

/**
 * Starts the tracker when the extension is signed in and stops it when it is not.
 *
 * @param signedIn - Whether the extension holds a session.
 */
function setTracking(signedIn: boolean): void {
  if (signedIn && !stopTracker) {
    stopTracker = startAutoTracker();
  } else if (!signedIn && stopTracker) {
    stopTracker();
    stopTracker = null;
  }
}

// The extension announces every sign-in and sign-out to the open Netflix tabs.
chrome.runtime.onMessage.addListener((message: SessionChangedMessage) => {
  if (message?.type === 'SESSION_CHANGED') setTracking(message.signedIn);
});

// Whether to track at all is decided once on load; the messages above keep it up to date.
chrome.runtime.sendMessage({ type: 'SESSION_STATUS' } satisfies InternalMessage, (res?: SessionStatus) => {
  if (chrome.runtime.lastError) {
    debug('service worker did not answer the session check:', chrome.runtime.lastError.message);
    return;
  }
  debug('content script loaded on', window.location.href, '- signed in:', res?.signedIn ?? false);
  setTracking(res?.signedIn ?? false);
});
