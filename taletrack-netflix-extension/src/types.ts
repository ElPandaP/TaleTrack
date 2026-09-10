export interface NetflixMedia {
  title: string;
  year?: number;
  type: 'movie' | 'series';
  genres: string[];
  duration?: string;
  description?: string;
  netflixUrl: string;
  extractedAt: string;
  /** Netflix UI language ("es"/"en"), from <html lang>. Used server-side for TMDB enrichment. */
  language?: string;

  // Extra fields for series
  season?: number;
  episode?: number;
  episodeTitle?: string;

  // Live playback state (only available while watching, on a /watch/ page)
  progressPercent?: number;   // 0–100, how far into the video you are
  positionSeconds?: number;   // current playback position
  runtimeSeconds?: number;    // total length of the movie / episode
}

// Popup ⇆ content script (manual "extract data" flow).
export interface ExtractDataMessage {
  action: 'extractData';
}

export interface ExtractDataResponse {
  success: boolean;
  data?: NetflixMedia;
  error?: string;
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
  /** Force a send regardless of the throttle (tab closing, playback ended). */
  flush?: boolean;
}

export type BgMessage =
  | { type: 'AUTH_STATE' }
  | { type: 'SIGN_IN' }
  | { type: 'SIGN_OUT' }
  | { type: 'TRACK_PROGRESS'; payload: TrackPayload };

export interface TrackResult {
  ok: boolean;
  /** 'unauthenticated' | 'throttled' | 'sent' | 'error' */
  reason?: string;
}
