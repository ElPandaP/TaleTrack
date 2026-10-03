/**
 * Build-time configuration shared by every context of the extension: the backend and web app
 * URLs, the debug switch and the auto-tracking poll interval.
 *
 * @module
 */

// Replaced at build time by build.ts (`define`). Without a build they are undefined and the
// constants below fall back to the deployed server.
declare const __TT_BACKEND_URL__: string;
declare const __TT_FRONTEND_URL__: string;
declare const __TT_DEBUG__: boolean;

/** Base URL of the TaleTrack backend API (set with `TT_BACKEND_URL`, defaults to production). */
export const BACKEND_URL: string =
  typeof __TT_BACKEND_URL__ !== 'undefined' ? __TT_BACKEND_URL__ : 'https://taletrack.app';

/** Base URL of the TaleTrack web app, which hosts the `/extension-auth` page (set with `TT_FRONTEND_URL`). */
export const FRONTEND_URL: string =
  typeof __TT_FRONTEND_URL__ !== 'undefined' ? __TT_FRONTEND_URL__ : 'https://taletrack.app';

/** Verbose console output, off unless built with `bun run build --debug`. */
const DEBUG: boolean = typeof __TT_DEBUG__ !== 'undefined' ? __TT_DEBUG__ : false;

/**
 * Logs to the console, prefixed with `[TaleTrack]`, only in debug builds.
 *
 * @param args - Values to log, as with `console.log`.
 */
export function debug(...args: unknown[]): void {
  if (DEBUG) console.log('[TaleTrack]', ...args);
}

/** How often, in milliseconds, the content script re-checks progress while a video is open. */
export const AUTO_POLL_MS = 15000;
