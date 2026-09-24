// Service worker: owns auth, refreshes tokens, and forwards watch progress to the backend.

import { apiFetch, getAuthState, getValidAccessToken, handleExternalAuthMessage, openSignInTab, signOut } from './auth';
import { FRONTEND_URL, debug } from './config';
import type { InternalMessage, WebAuthMessage, TrackPayload, TrackResult } from './types';

const FRONTEND_ORIGIN = new URL(FRONTEND_URL).origin;

const PROGRESS_STEP = 2;          // only report every 2% of forward movement
const NEARLY_DONE = 95;           // ...but always report crossing this
const SENT_PREFIX = 'sent:';      // chrome.storage.session key per videoId

// The tokens live in chrome.storage.local, which content scripts (running inside Netflix's
// pages) can read by default. Only the extension's own pages and service worker need them.
chrome.storage.local.setAccessLevel({ accessLevel: 'TRUSTED_CONTEXTS' }).catch(() => {
  /* older Chrome without storage access levels */
});

// Keep the access token warm while the browser is open.
chrome.runtime.onInstalled.addListener(() => {
  chrome.alarms.create('tt-refresh', { periodInMinutes: 30 });
});
chrome.runtime.onStartup.addListener(() => {
  chrome.alarms.create('tt-refresh', { periodInMinutes: 30 });
});
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === 'tt-refresh') void getValidAccessToken();
});

// Message router.
chrome.runtime.onMessage.addListener((message: InternalMessage, _sender, sendResponse) => {
  (async () => {
    try {
      switch (message.type) {
        case 'AUTH_STATE':
          sendResponse(await getAuthState());
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
  return true; // async response
});

// Auth bridge: /extension-auth posts the token pair here. externally_connectable
// in manifest.json (built from TT_FRONTEND_URL, never a wildcard) restricts who
// can reach this listener at all, but re-check origin and message shape anyway.
chrome.runtime.onMessageExternal.addListener((message: unknown, sender, sendResponse) => {
  if (sender.origin !== FRONTEND_ORIGIN) {
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
    sendResponse({ ok: false, reason: 'invalid-message' });
    return false;
  }

  handleExternalAuthMessage(m.access, m.refresh, m.expiresIn)
    .then(() => sendResponse({ ok: true }))
    .catch(() => sendResponse({ ok: false, reason: 'error' }));
  return true; // async response
});

// Progress reporting.
async function lastSent(videoId: string): Promise<number | null> {
  const o = await chrome.storage.session.get(SENT_PREFIX + videoId);
  const v = o[SENT_PREFIX + videoId];
  return typeof v === 'number' ? v : null;
}

async function rememberSent(videoId: string, progress: number): Promise<void> {
  await chrome.storage.session.set({ [SENT_PREFIX + videoId]: progress });
}

function shouldSend(prev: number | null, next: number, flush: boolean): boolean {
  if (flush) return true;
  if (prev === null) return true;
  if (next - prev >= PROGRESS_STEP) return true;
  if (prev < NEARLY_DONE && next >= NEARLY_DONE) return true;
  return false;
}

async function track(payload: TrackPayload): Promise<TrackResult> {
  const { videoId, media, progressPercent, flush = false } = payload;

  const token = await getValidAccessToken();
  if (!token) return { ok: false, reason: 'unauthenticated' };

  const prev = await lastSent(videoId);
  if (!shouldSend(prev, progressPercent, flush)) return { ok: true, reason: 'throttled' };

  const minutes =
    media.runtimeSeconds && media.runtimeSeconds > 0
      ? Math.max(1, Math.round(media.runtimeSeconds / 60))
      : undefined;

  let path: string;
  let body: Record<string, unknown>;

  if (media.type === 'series') {
    if (media.season === undefined || media.episode === undefined) {
      return { ok: false, reason: 'incomplete-series-metadata' };
    }
    path = '/api/tracking/series';
    body = {
      Title: media.title,
      Season: media.season,
      Episode: media.episode,
      Minutes: minutes,
      Progress: progressPercent,
      Language: media.language,
    };
  } else {
    path = '/api/tracking/movies';
    body = { Title: media.title, Minutes: minutes, Progress: progressPercent, Language: media.language };
  }

  debug(`sending tracking event → ${path}`, body);

  const res = await apiFetch(path, { method: 'POST', body: JSON.stringify(body) });
  if (!res) return { ok: false, reason: 'unauthenticated' };
  if (!res.ok) {
    console.warn('TaleTrack tracking failed:', res.status, await res.text().catch(() => ''));
    return { ok: false, reason: 'error' };
  }

  await rememberSent(videoId, progressPercent);
  return { ok: true, reason: 'sent' };
}
