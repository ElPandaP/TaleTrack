// Runs in the page (MAIN) context — has access to window.netflix.

import type { NetflixPageData, NetflixPlayerMetadata } from './types';

// The slice of Netflix's internal API this script touches. None of it is documented.
interface NetflixVideoPlayer {
  getAllPlayerSessionIds?: () => unknown[];
  getVideoPlayerBySessionId: (
    sessionId: unknown,
  ) => { getDuration?: () => number; getMetadata?: () => NetflixPlayerMetadata } | undefined;
  getVideoMetadataBySessionId?: (sessionId: unknown) => { getMetadata?: () => NetflixPlayerMetadata } | undefined;
}

interface NetflixGlobal {
  appContext?: {
    state?: { playerApp?: { getAPI?: () => { videoPlayer?: NetflixVideoPlayer } | undefined } };
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

const netflix = (): NetflixGlobal | undefined => (window as Window & { netflix?: NetflixGlobal }).netflix;

// Pull the total duration (milliseconds) and the raw player metadata (has
// seasons/episodes) from Netflix's own player API.
function getPlayerState(): NonNullable<NetflixPageData['player']> | null {
  try {
    const videoPlayer = netflix()?.appContext?.state?.playerApp?.getAPI?.()?.videoPlayer;
    if (!videoPlayer) return null;

    const sessionIds = videoPlayer.getAllPlayerSessionIds?.() ?? [];
    const sessionId = sessionIds.find((id) => String(id).startsWith('watch-')) ?? sessionIds[0];
    if (!sessionId) return null;

    const player = videoPlayer.getVideoPlayerBySessionId(sessionId);
    if (!player) return null;

    const duration = typeof player.getDuration === 'function' ? player.getDuration() : null;

    // Both come back undefined on current Netflix builds; kept in case a
    // future/regional build restores them — richer metadata otherwise means scraping the DOM.
    let metadata: NetflixPlayerMetadata | null = null;
    try {
      if (typeof player.getMetadata === 'function') {
        metadata = player.getMetadata();
      } else if (typeof videoPlayer.getVideoMetadataBySessionId === 'function') {
        metadata = videoPlayer.getVideoMetadataBySessionId(sessionId)?.getMetadata?.() ?? null;
      }
    } catch {
      // metadata is optional
    }

    return {
      duration: typeof duration === 'number' && Number.isFinite(duration) && duration > 0 ? duration : null,
      metadata,
    };
  } catch {
    return null;
  }
}

function readPageData(): NetflixPageData | null {
  let data: NetflixPageData | null = null;

  const videoId = window.location.href.match(/\/watch\/(\d+)/)?.[1];
  if (videoId) {
    const videoData = netflix()?.falcorCache?.videos?.[videoId];
    if (videoData?.summary?.value) {
      data = {
        videoId,
        summary: videoData.summary.value,
        runtime: videoData.runtime?.value || null,
        creditsOffset: videoData.creditsOffset?.value || null,
      };
    }
  }

  const player = getPlayerState();
  if (player) data = { ...data, player };

  return data;
}

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
