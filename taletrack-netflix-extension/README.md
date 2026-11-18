# TaleTrack — Netflix extension

Chrome extension (Manifest V3) that detects what you are watching on Netflix and tracks it in your
TaleTrack account automatically, without logging it by hand.

## Install

Download `taletrack-netflix-extension.zip` from the
[latest release](https://github.com/ElPandaP/TaleTrack/releases/latest) and unzip it. In Chrome,
open `chrome://extensions`, turn on *Developer mode*, click *Load unpacked* and choose the unzipped
folder. That build points to [taletrack.app](https://taletrack.app) (currently offline). To build it
yourself or point it at another server, see
[Building](#building).

## How it works

Netflix has no public API, so the extension combines several sources with fallbacks, all in
`src/netflix-extract.ts`:

- The **metadata the Netflix player has already loaded** for the video
  (`window.netflix.appContext.state.playerApp.getState().videoPlayer.videoMetadata[id]`): title,
  type, seasons and episodes. It is the main source. It is read from a script injected into the
  `MAIN` world (`src/injected.ts`), because a regular content script cannot touch the page's
  JavaScript, only its DOM. That script only reads data that is already loaded: it does not call
  player methods or make requests to Netflix.
- Netflix's **Falcor** cache (`window.netflix.falcorCache`), for runtime, credits and
  season/episode when the metadata does not have them.
- The page DOM as a last resort: the title bar (with several selectors, because Netflix changes its
  classes often) and the `<video>` element, which gives position and duration.
- `<html lang>`, to know whether the interface is in Spanish or English. It is sent to the backend so
  it can enrich the item with TMDB (poster, synopsis, runtime, title in the other language) without
  relying on the title matching word for word if you switch Netflix's language mid-season.

Season and episode are only taken from the first two sources (player metadata and Falcor). If
neither has them, the series is not recorded.

`src/content.ts` decides **when** to call that extraction. It only works with the extension signed
in: on load it asks the service worker whether there is a session, and the extension notifies open
Netflix tabs whenever a session starts or ends. With a session, it polls every 15 s, only while you
are on a `/watch/` page, and also reacts immediately to URL changes (Netflix is a single-page app, so
going from `/browse` to `/watch/` does not reload the script) and to the `loadedmetadata` event.
If there is no title yet, it retries up to 10 times, once per second, before giving up until the
next poll (in case it has to fall back to the title bar, which is only visible for the first few
seconds). Once the title of a video is resolved, it is cached and not read from the DOM again while
you stay on the same episode or film. A title from the player metadata is used as is; only text
scraped from the DOM is stripped of episode markers, and only for series, because a colon or dash
in a film title (`Mission: Impossible`) is part of its name.

`content.ts` also decides when something counts as watched: on entering the margin before the
credits (the `creditsOffset` Netflix provides, requested again on every poll until it shows up)
progress becomes 100 %, without waiting for the video to actually end.

Progress is sent to `POST /api/tracking/movies` or `/api/tracking/series` through the service worker
(`src/background.ts`), which throttles updates so the backend is not flooded: the first one for each
video, then only every 2 % of change (forwards or backwards) or when crossing 95 %. The filter is
skipped (forced send) when the tab is closed or hidden, when the video ends, when it changes and
when you leave the player. In the last two cases the `<video>` can no longer be read, so the last
reading saved by `content.ts` is sent. There is no offline queue: if an update fails, the next poll
reports the current state.

## Structure

```
src/
├── content.ts           # auto-tracking loop: polling, title, credits, sending progress
├── netflix-extract.ts   # all the "what is on this Netflix page" logic
├── injected.ts          # runs in the page's MAIN world (access to window.netflix)
├── background.ts        # service worker: update throttling, token refresh
├── auth.ts              # JWT/refresh token lifecycle (chrome.storage.local)
├── popup.ts             # popup UI: session state, sign in and sign out
├── config.ts            # BACKEND_URL / FRONTEND_URL / DEBUG (build time) and debug()
└── types.ts             # types shared between the different contexts
public/
├── popup.html           # popup markup and styles (the web app's tokens and fonts)
└── fonts/               # Geist and Cormorant Garamond as woff2, with their OFL licenses
tests/
└── netflix-extract.test.ts  # title cleaning and episode marker parsing (bun test)
```

## Auth

Clicking "Sign in" in the popup opens a regular tab at `FRONTEND_URL/extension-auth` and closes the
popup (it does not wait for the result; reopening it shows the session). It uses
`chrome.tabs.create`, not `chrome.identity`: that API sits behind an "allow Google sign-in" setting
in Brave that has nothing to do with this flow, so it is avoided entirely. That page, already
signed in to the web app, asks you to confirm, requests a separate token pair from the backend
(`POST /api/auth/extension-grant`, which shows up as the **"Netflix extension"** session in your
profile) and passes it to the extension with `chrome.runtime.sendMessage(EXTENSION_ID, ...)`. This
is allowed because the `FRONTEND_URL` origin is declared in `externally_connectable` in
`manifest.json` (generated at build time from `TT_FRONTEND_URL`, never a wildcard).
`background.ts` checks the sender's origin and the message shape again before storing anything.
Tokens are kept in `chrome.storage.local` and refreshed automatically.

The `"key"` field of `manifest.json` fixes the extension id (`kbhjoofgffidokbnlklekelkpdhcllih`) so
it does not change between reloads or machines. The web app needs to know it in advance
(`NEXT_PUBLIC_EXTENSION_ID`, with that same default) to be able to message it.

## Building

```bash
bun install
bun build.ts
```

By default it targets the production server (`https://taletrack.app`, set in `build.ts` and as a
fallback in `src/config.ts`). For local development:

```bash
TT_BACKEND_URL=http://localhost:8080 TT_FRONTEND_URL=http://localhost:8090 bun build.ts
```

With `bun run build --debug` the extension writes detailed traces to the console (`debug()` in
`config.ts`); without it, it only logs errors. The build generates `dist/` with the scripts,
`popup.html`, the fonts and `manifest.json`, to which it adds `externally_connectable` and
`host_permissions` based on the chosen URLs. To load it in Chrome: `chrome://extensions` → turn on
*Developer mode* → *Load unpacked* → choose the `dist/` folder. After rebuilding, click reload in
`chrome://extensions`.

The build's `host_permissions` are always `netflix.com` plus the origin of `TT_BACKEND_URL`, so a
production build does not ask for `localhost` access and a local one accepts whatever port you give
it. There are no icons yet (`icons` in the manifest and the PNGs in `public/`), which the Chrome Web
Store requires.

## Tests

```bash
bun test
```

They cover title cleaning and episode marker parsing (`tests/netflix-extract.test.ts`); the rest
depends on the DOM and the Netflix player and can only be tested by hand.
