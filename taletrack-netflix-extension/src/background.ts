// Service worker: owns auth, refreshes tokens, and forwards watch progress to the backend.

import { apiFetch, getAuthState, getValidAccessToken, signIn, signOut } from './auth';
import type { BgMessage, TrackPayload, TrackResult } from './types';

const PROGRESS_STEP = 5;          // only report every 5% of movement
const NEARLY_DONE = 95;           // ...but always report crossing this
const SENT_PREFIX = 'sent:';      // chrome.storage.session key per videoId

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
chrome.runtime.onMessage.addListener((message: BgMessage, _sender, sendResponse) => {
  (async () => {
    try {
      switch (message.type) {
        case 'AUTH_STATE':
          sendResponse(await getAuthState());
          break;
        case 'SIGN_IN': {
          const ok = await signIn().catch(() => false);
          sendResponse({ ok, state: ok ? await getAuthState() : { authenticated: false } });
          break;
        }
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
  if (Math.abs(next - prev) >= PROGRESS_STEP) return true;
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
      EpisodeTitle: media.episodeTitle,
      Minutes: minutes,
      Progress: progressPercent,
      Language: media.language,
    };
  } else {
    path = '/api/tracking/movies';
    body = { Title: media.title, Minutes: minutes, Progress: progressPercent, Language: media.language };
  }

  const res = await apiFetch(path, { method: 'POST', body: JSON.stringify(body) });
  if (!res) return { ok: false, reason: 'unauthenticated' };
  if (!res.ok) {
    console.warn('TaleTrack tracking failed:', res.status, await res.text().catch(() => ''));
    return { ok: false, reason: 'error' };
  }

  await rememberSent(videoId, progressPercent);
  return { ok: true, reason: 'sent' };
}
