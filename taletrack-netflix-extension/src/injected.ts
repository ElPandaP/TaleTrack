/**
 * Page script, injected into Netflix's own JavaScript context (the `MAIN` world).
 *
 * A content script can see the page's DOM but not its JavaScript objects, so this small script
 * runs inside the page to read `window.netflix`: the player's stored metadata and the Falcor
 * cache. It answers `GET_NETFLIX_DATA` requests posted by the content script with a
 * `NETFLIX_DATA_RESPONSE` carrying a {@link NetflixPageData}.
 *
 * It only reads data the page has already loaded for its own player: it calls no method of
 * Netflix's player and makes no requests.
 *
 * @module
 */

import type { NetflixPageData, NetflixPlayerMetadata } from './types';
import { debug } from './config';

/** An entry of the player app's per-video metadata store; its `_metadataObject` holds the raw `{ video }`. */
interface NetflixVideoMetadataEntry {
  _metadataObject?: NetflixPlayerMetadata;
}

/** The slice of Netflix's page state (`window.netflix`) this script reads. None of it is documented. */
interface NetflixGlobal {
  appContext?: {
    state?: {
      playerApp?: {
        getState?: () => { videoPlayer?: { videoMetadata?: Record<string, NetflixVideoMetadataEntry | undefined> } };
      };
    };
  };
  falcorCache?: {
    videos?: Record<
      string,
      | {
          summary?: { value?: NonNullable<NetflixPageData['summary']> };
          runtime?: { value?: number };
          creditsOffset?: { value?: number };
        }
      | undefined
    >;
  };
}

/** The page's `window.netflix` object, if Netflix has set it up yet. */
const netflix = (): NetflixGlobal | undefined => (window as Window & { netflix?: NetflixGlobal }).netflix;

/**
 * The player app's stored metadata for a video: title, type, seasons/episodes and credits offsets.
 *
 * @param videoId - Netflix video id from the /watch/ URL.
 * @returns The metadata, or null when the player has none for that video (or reading it fails).
 */
function getStoredMetadata(videoId: string): NetflixPlayerMetadata | null {
  try {
    const entry = netflix()?.appContext?.state?.playerApp?.getState?.()?.videoPlayer?.videoMetadata?.[videoId];
    if (!entry) {
      debug('[page] no stored player metadata for video', videoId);
      return null;
    }
    const metadata = entry._metadataObject ?? null;
    debug('[page] stored player metadata for video', videoId, metadata ?? '(no _metadataObject)');
    return metadata;
  } catch (err) {
    debug('[page] reading stored player metadata threw:', err);
    return null;
  }
}

/**
 * Collects the player metadata and the Falcor cache entry for the video in the current /watch/ URL.
 *
 * @returns The data found, or null when not on a /watch/ page or when neither source knows the video.
 */
function readPageData(): NetflixPageData | null {
  const videoId = window.location.href.match(/\/watch\/(\d+)/)?.[1];
  if (!videoId) return null;

  const data: NetflixPageData = { videoId, metadata: getStoredMetadata(videoId) };

  // Each field is read on its own: the cache can hold a video's runtime and credits
  // offset without its summary.
  const videoData = netflix()?.falcorCache?.videos?.[videoId];
  if (videoData) {
    data.summary = videoData.summary?.value;
    data.runtime = videoData.runtime?.value || null;
    data.creditsOffset = videoData.creditsOffset?.value || null;
  } else {
    const cached = Object.keys(netflix()?.falcorCache?.videos ?? {}).length;
    debug('[page] video', videoId, `not in the Falcor cache (cache has ${cached} videos)`);
  }

  return data.metadata || videoData ? data : null;
}

// Answers the content script's requests. Both sides share the page's window, so messages from
// any other source are ignored, and replies are posted only to Netflix's own origin.
window.addEventListener('message', (event) => {
  if (event.source !== window) return;
  if (event.data?.type !== 'GET_NETFLIX_DATA') return;

  const requestId: unknown = event.data.requestId;
  try {
    const data = readPageData();
    window.postMessage({ type: 'NETFLIX_DATA_RESPONSE', requestId, success: !!data, data }, window.location.origin);
  } catch (error) {
    window.postMessage(
      {
        type: 'NETFLIX_DATA_RESPONSE',
        requestId,
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error',
      },
      window.location.origin,
    );
  }
});
