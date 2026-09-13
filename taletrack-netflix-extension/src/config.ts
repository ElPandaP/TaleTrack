// Injected at build time by build.ts (`--define`). Falls back to the deployed server.
declare const __TT_BACKEND_URL__: string;
declare const __TT_FRONTEND_URL__: string;

export const BACKEND_URL: string =
  typeof __TT_BACKEND_URL__ !== 'undefined' ? __TT_BACKEND_URL__ : 'https://taletrack.app';

export const FRONTEND_URL: string =
  typeof __TT_FRONTEND_URL__ !== 'undefined' ? __TT_FRONTEND_URL__ : 'https://taletrack.app';

/** How often content.ts's auto-tracker re-checks progress/resolves the title. */
export const AUTO_POLL_MS = 15000;
