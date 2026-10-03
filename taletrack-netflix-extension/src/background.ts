/**
 * Background service worker of the extension.
 *
 * It is the only context that talks to the TaleTrack backend. It answers the popup's session
 * requests, receives the token pair from the web app's `/extension-auth` page, keeps the access
 * token fresh, and forwards the content script's progress reports to the tracking endpoints,
 * throttling them so the backend gets one write per meaningful change.
 *
 * @remarks
 * As a Manifest V3 service worker it can be stopped whenever it is idle, so it keeps no state in
 * memory between events: the tokens live in `chrome.storage.local` and the last progress sent per
 * video in `chrome.storage.session`.
 *
 * @module
 */

import { apiFetch, getAuthState, getValidAccessToken, hasSession, handleExternalAuthMessage, openSignInTab, signOut } from './auth';
import { FRONTEND_URL, debug } from './config';
import type { InternalMessage, SessionStatus, WebAuthMessage, TrackPayload, TrackResult } from './types';

/** The only origin allowed to send the token pair. */
const FRONTEND_ORIGIN = new URL(FRONTEND_URL).origin;

/** Minimum change in progress, in percentage points and in either direction, worth reporting. */
const PROGRESS_STEP = 2;
/** Progress that is always reported when crossed, even within the step. */
const NEARLY_DONE = 95;
/** Prefix of the `chrome.storage.session` key that holds the last progress sent for each video id. */
const SENT_PREFIX = 'sent:';

// By default content scripts, which run inside Netflix's pages, can read chrome.storage.local.
// The tokens kept there are only needed by the extension's own pages and this service worker.
chrome.storage.local.setAccessLevel({ accessLevel: 'TRUSTED_CONTEXTS' }).catch(() => {
  /* older Chrome without storage access levels */
});

// Keep the access token fresh while the browser is open.
chrome.runtime.onInstalled.addListener(() => {
  chrome.alarms.create('tt-refresh', { periodInMinutes: 30 });
});
chrome.runtime.onStartup.addListener(() => {
  chrome.alarms.create('tt-refresh', { periodInMinutes: 30 });
});
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === 'tt-refresh') void getValidAccessToken();
});

// Routes the popup's and content script's messages (see InternalMessage).
chrome.runtime.onMessage.addListener((message: InternalMessage, _sender, sendResponse) => {
  debug('message', message.type, message.type === 'TRACK_PROGRESS' ? message.payload : '');
  (async () => {
    try {
      switch (message.type) {
        case 'AUTH_STATE':
          sendResponse(await getAuthState());
          break;
        case 'SESSION_STATUS':
          sendResponse({ signedIn: await hasSession() } satisfies SessionStatus);
          break;
        case 'SIGN_IN':
          openSignInTab();
          sendResponse({ ok: true });
          break;
        case 'SIGN_OUT':
          await signOut();
          sendResponse({ ok: true });
          break;
        case 'TRACK_PROGRESS':
          sendResponse(await track(message.payload));
          break;
        default:
          sendResponse({ ok: false, reason: 'unknown-message' });
      }
    } catch (err) {
      console.error('TaleTrack background error:', err);
      sendResponse({ ok: false, reason: 'error' });
    }
  })();
  return true; // keeps the channel open for the async sendResponse
});

// Receives the token pair from the /extension-auth page. externally_connectable in the built
// manifest.json (the exact TT_FRONTEND_URL origin, never a wildcard) already limits who can reach
// this listener; the origin and the message shape are checked again here anyway.
chrome.runtime.onMessageExternal.addListener((message: unknown, sender, sendResponse) => {
  debug('external message from', sender.origin);
  if (sender.origin !== FRONTEND_ORIGIN) {
    debug('rejected: expected origin', FRONTEND_ORIGIN);
    sendResponse({ ok: false, reason: 'untrusted-origin' });
    return false;
  }

  const m = message as Partial<WebAuthMessage> | null;
  if (
    !m ||
    m.type !== 'TALETRACK_AUTH' ||
    typeof m.access !== 'string' ||
    !m.access ||
    typeof m.refresh !== 'string' ||
    !m.refresh ||
    (m.expiresIn !== undefined && typeof m.expiresIn !== 'number')
  ) {
    debug('rejected: not a valid TALETRACK_AUTH message');
    sendResponse({ ok: false, reason: 'invalid-message' });
    return false;
  }

  handleExternalAuthMessage(m.access, m.refresh, m.expiresIn)
    .then(() => {
      debug('signed in: tokens stored');
      sendResponse({ ok: true });
    })
    .catch((err) => {
      console.error('TaleTrack: could not store the tokens:', err);
      sendResponse({ ok: false, reason: 'error' });
    });
  return true; // keeps the channel open for the async sendResponse
});

/** The last progress sent for a video in this browser session, or null if none was sent yet. */
async function lastSent(videoId: string): Promise<number | null> {
  const o = await chrome.storage.session.get(SENT_PREFIX + videoId);
  const v = o[SENT_PREFIX + videoId];
  return typeof v === 'number' ? v : null;
}

/** Records the progress just sent for a video. */
async function rememberSent(videoId: string, progress: number): Promise<void> {
  await chrome.storage.session.set({ [SENT_PREFIX + videoId]: progress });
}

/**
 * The throttle: whether a new progress reading is worth sending.
 *
 * @param prev - Last progress sent for the video, or null if none.
 * @param next - New progress reading.
 * @param flush - Forces a send.
 * @returns True for the first reading of a video, a forced send, a change of at least
 * `PROGRESS_STEP` points, or crossing `NEARLY_DONE`.
 */
function shouldSend(prev: number | null, next: number, flush: boolean): boolean {
  if (flush) return true;
  if (prev === null) return true;
  if (Math.abs(next - prev) >= PROGRESS_STEP) return true;
  if (prev < NEARLY_DONE && next >= NEARLY_DONE) return true;
  return false;
}

/**
 * Handles a `TRACK_PROGRESS` report: applies the throttle and posts the progress to
 * `POST /api/tracking/movies` or `POST /api/tracking/series`.
 *
 * @param payload - The content script's report.
 * @returns Whether it was sent, skipped by the throttle, or why it failed.
 */
async function track(payload: TrackPayload): Promise<TrackResult> {
  const { videoId, media, progressPercent, flush = false } = payload;

  const token = await getValidAccessToken();
  if (!token) {
    debug('not sending: no valid token (signed out, or the refresh failed)');
    return { ok: false, reason: 'unauthenticated' };
  }

  const prev = await lastSent(videoId);
  if (!shouldSend(prev, progressPercent, flush)) {
    debug(`throttled: last sent ${prev}%, now ${progressPercent}% (sends every ${PROGRESS_STEP}%)`);
    return { ok: true, reason: 'throttled' };
  }

  const minutes =
    media.runtimeSeconds && media.runtimeSeconds > 0
      ? Math.max(1, Math.round(media.runtimeSeconds / 60))
      : undefined;

  let path: string;
  let body: Record<string, unknown>;

  if (media.type === 'series') {
    if (media.season === undefined || media.episode === undefined) {
      debug('not sending: series without season/episode', media);
      return { ok: false, reason: 'incomplete-series-metadata' };
    }
    path = '/api/tracking/series';
    body = {
      Title: media.title,
      Season: media.season,
      Episode: media.episode,
      Progress: progressPercent,
      Language: media.language,
    };
  } else {
    path = '/api/tracking/movies';
    body = { Title: media.title, Minutes: minutes, Progress: progressPercent, Language: media.language };
  }

  debug(`sending tracking event → ${path}`, body);

  const res = await apiFetch(path, { method: 'POST', body: JSON.stringify(body) });
  if (!res) {
    debug('backend call skipped: signed out while sending');
    return { ok: false, reason: 'unauthenticated' };
  }
  if (!res.ok) {
    console.warn('TaleTrack tracking failed:', res.status, await res.text().catch(() => ''));
    return { ok: false, reason: 'error' };
  }

  debug(`sent: backend answered ${res.status}`);
  await rememberSent(videoId, progressPercent);
  return { ok: true, reason: 'sent' };
}
