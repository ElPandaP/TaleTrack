export interface NetflixMedia {
  title: string;
  type: 'movie' | 'series';
  /** Netflix UI language ("es"/"en"), from <html lang>. Used server-side for TMDB enrichment. */
  language?: string;

  // Extra fields for series
  season?: number;
  episode?: number;

  /** Total length of the movie / episode (only available while watching, on a /watch/ page). */
  runtimeSeconds?: number;
}

// What injected.ts (MAIN world) reads from window.netflix and hands to the content script.
// Netflix's internals change between builds and regions, so every field is optional.
export interface NetflixEpisodeRef {
  id?: number | string;
  seq?: number;
  episode?: number;
}

export interface NetflixSeasonRef {
  seq?: number;
  season?: number;
  episodes?: NetflixEpisodeRef[];
}

export interface NetflixVideoMetadata {
  title?: string;
  type?: string;
  seasons?: NetflixSeasonRef[];
  currentEpisode?: number | string;
  episodeId?: number | string;
}

export interface NetflixPlayerMetadata {
  video?: NetflixVideoMetadata;
  _metadata?: { video?: NetflixVideoMetadata };
}

export interface NetflixPageData {
  videoId?: string;
  summary?: { title?: string; type?: string; season?: number; episode?: number };
  /** Runtime in seconds, from the Falcor cache. */
  runtime?: number | null;
  /** Where the credits start, in seconds, from the Falcor cache. */
  creditsOffset?: number | null;
  player?: { duration: number | null; metadata: NetflixPlayerMetadata | null };
}

// Popup / content ⇆ background service worker.
export interface AuthState {
  authenticated: boolean;
  user?: { username: string; email: string };
}

export interface TrackPayload {
  videoId: string;
  media: NetflixMedia;
  progressPercent: number;
  /** Force a send regardless of the throttle (tab hidden or closing, playback ended, video switched). */
  flush?: boolean;
}

/** Messages the popup and content script send to the background via chrome.runtime.sendMessage. */
export type InternalMessage =
  | { type: 'AUTH_STATE' }
  | { type: 'SIGN_IN' }
  | { type: 'SIGN_OUT' }
  | { type: 'TRACK_PROGRESS'; payload: TrackPayload };

/** Sent by the taletrack-frontend /extension-auth page (not by the extension's own scripts) once
 *  it has a fresh token pair for this device — see externally_connectable in manifest.json. */
export interface WebAuthMessage {
  type: 'TALETRACK_AUTH';
  access: string;
  refresh: string;
  expiresIn?: number;
}

export interface TrackResult {
  ok: boolean;
  reason?: 'unauthenticated' | 'throttled' | 'sent' | 'error' | 'incomplete-series-metadata' | 'unknown-message';
}
