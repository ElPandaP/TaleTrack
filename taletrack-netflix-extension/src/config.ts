// Injected at build time by build.ts (`--define`). Falls back to the deployed server.
declare const __TT_BACKEND_URL__: string;
declare const __TT_FRONTEND_URL__: string;
declare const __TT_DEBUG__: boolean;

export const BACKEND_URL: string =
  typeof __TT_BACKEND_URL__ !== 'undefined' ? __TT_BACKEND_URL__ : 'https://taletrack.app';

export const FRONTEND_URL: string =
  typeof __TT_FRONTEND_URL__ !== 'undefined' ? __TT_FRONTEND_URL__ : 'https://taletrack.app';

/** Verbose console output, off unless built with `TT_DEBUG=1 bun build.ts`. */
export const DEBUG: boolean = typeof __TT_DEBUG__ !== 'undefined' ? __TT_DEBUG__ : false;

/** console.log that only speaks in debug builds. */
export function debug(...args: unknown[]): void {
  if (DEBUG) console.log('[TaleTrack]', ...args);
}

/** How often content.ts's auto-tracker re-checks progress/resolves the title. */
export const AUTO_POLL_MS = 15000;
