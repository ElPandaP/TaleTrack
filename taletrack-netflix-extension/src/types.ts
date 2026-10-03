/**
 * Types shared between the extension's contexts: the injected page script, the content script,
 * the background service worker and the popup.
 *
 * They cover three things: the media detected on Netflix, the raw player data read from the page,
 * and the messages exchanged between the contexts (and with the web app's `/extension-auth` page).
 *
 * @module
 */

/** A movie or series episode detected on Netflix, ready to be reported to the backend. */
export interface NetflixMedia {
  /** Movie title, or the show name for a series (without episode marker or episode name). */
  title: string;
  /** Whether it is a movie or an episode of a series. */
  type: 'movie' | 'series';
  /** Netflix UI language ("es"/"en"), from `<html lang>`. Used server-side for TMDB enrichment. */
  language?: string;

  /** Season number. Only for series. */
  season?: number;
  /** Episode number within the season. Only for series. */
  episode?: number;

  /** Total length of the movie / episode (only available while watching, on a /watch/ page). */
  runtimeSeconds?: number;
}

/**
 * An episode as listed in Netflix's player metadata.
 *
 * @remarks
 * This and the following `Netflix*` types describe what the injected page script reads from
 * `window.netflix` and hands to the content script. Netflix's internals are undocumented and
 * change between builds and regions, so every field is optional.
 */
export interface NetflixEpisodeRef {
  /** Netflix's video id for the episode (a number or a string, depending on the build). */
  id?: number | string;
  /** Episode number within its season. */
  seq?: number;
  /** Episode number, under the name some builds use instead of `seq`. */
  episode?: number;
  /** Where this episode's credits start, in seconds. */
  creditsOffset?: number;
}

/** A season as listed in Netflix's player metadata. */
export interface NetflixSeasonRef {
  /** Season number. */
  seq?: number;
  /** Season number, under the name some builds use instead of `seq`. */
  season?: number;
  /** The season's episodes. */
  episodes?: NetflixEpisodeRef[];
}

/** The video part of Netflix's player metadata: a movie, or a show with its seasons. */
export interface NetflixVideoMetadata {
  /** Movie or show title, without episode information. */
  title?: string;
  /** Netflix's own kind, such as "movie" or "show". */
  type?: string;
  /** Seasons and episodes, present for shows. */
  seasons?: NetflixSeasonRef[];
  /** Id of the episode being played (one of the two names Netflix uses for it). */
  currentEpisode?: number | string;
  /** Id of the episode being played (the other name Netflix uses for it). */
  episodeId?: number | string;
  /** Where a movie's credits start, in seconds. */
  creditsOffset?: number;
}

/** The raw metadata Netflix's player keeps per video. */
export interface NetflixPlayerMetadata {
  /** The movie or show the metadata describes. */
  video?: NetflixVideoMetadata;
}

/** Everything the injected page script could read about the video in the current /watch/ URL. */
export interface NetflixPageData {
  /** The id in the /watch/ URL the data belongs to. */
  videoId?: string;
  /** Short summary from the Falcor cache: title, kind and, for episodes, season and episode. */
  summary?: { title?: string; type?: string; season?: number; episode?: number };
  /** Runtime in seconds, from the Falcor cache. */
  runtime?: number | null;
  /** Where the credits start, in seconds, from the Falcor cache. */
  creditsOffset?: number | null;
  /** The player's stored metadata for the video (title, seasons, episodes). */
  metadata?: NetflixPlayerMetadata | null;
}

/** Whether the extension holds a session, as shown in the popup. */
export interface AuthState {
  /** True when the extension has tokens it can use. */
  authenticated: boolean;
  /** The signed-in account, when the backend could be asked for it. */
  user?: { username: string; email: string };
}

/** One progress report from the content script for the background service worker to send. */
export interface TrackPayload {
  /** Netflix video id, used to throttle reports per video. */
  videoId: string;
  /** What is being watched. */
  media: NetflixMedia;
  /** Progress through the movie or episode, 0 to 100. */
  progressPercent: number;
  /** Force a send regardless of the throttle (tab hidden or closing, playback ended, video switched). */
  flush?: boolean;
}

/**
 * Messages the popup and content script send to the background service worker via
 * `chrome.runtime.sendMessage`.
 *
 * - `AUTH_STATE`: asks for the current {@link AuthState}.
 * - `SESSION_STATUS`: asks whether the extension is signed in, answered with a {@link SessionStatus}.
 * - `SIGN_IN`: opens the web app's sign-in page in a new tab.
 * - `SIGN_OUT`: ends the session on the backend and drops the local tokens.
 * - `TRACK_PROGRESS`: reports watch progress, answered with a {@link TrackResult}.
 */
export type InternalMessage =
  | { type: 'AUTH_STATE' }
  | { type: 'SESSION_STATUS' }
  | { type: 'SIGN_IN' }
  | { type: 'SIGN_OUT' }
  | { type: 'TRACK_PROGRESS'; payload: TrackPayload };

/** The background service worker's answer to a `SESSION_STATUS` message. */
export interface SessionStatus {
  /** Whether the extension holds a session. */
  signedIn: boolean;
}

/**
 * Sent by the extension to the content script of every open Netflix tab when the extension signs
 * in or out, so tracking starts or stops without reloading the tab.
 */
export interface SessionChangedMessage {
  /** Message tag. */
  type: 'SESSION_CHANGED';
  /** Whether the extension is now signed in. */
  signedIn: boolean;
}

/**
 * The token pair sent by the taletrack-frontend `/extension-auth` page (not by the extension's own
 * scripts). Only that page's origin can send it, as declared in `externally_connectable` in the
 * built `manifest.json`.
 */
export interface WebAuthMessage {
  /** Message tag. */
  type: 'TALETRACK_AUTH';
  /** Access token (JWT). */
  access: string;
  /** Refresh token for this device's own session. */
  refresh: string;
  /** Access token lifetime in seconds. */
  expiresIn?: number;
}

/** The background service worker's answer to a `TRACK_PROGRESS` message. */
export interface TrackResult {
  /** False when the report could not be sent. */
  ok: boolean;
  /**
   * What happened: sent, skipped by the throttle, or why it failed (no session, backend error,
   * a series without season or episode, an unknown message type).
   */
  reason?: 'unauthenticated' | 'throttled' | 'sent' | 'error' | 'incomplete-series-metadata' | 'unknown-message';
}
