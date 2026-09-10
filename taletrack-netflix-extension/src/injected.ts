// Runs in the page (MAIN) context — has access to window.netflix.

// Pull live playback state from Netflix's own player API.
// Returns times in milliseconds, plus the raw player metadata (has seasons/episodes).
function getPlayerState(): {
  currentTime: number | null;
  duration: number | null;
  metadata: any | null;
} | null {
  try {
    // @ts-ignore - window.netflix is injected by the site
    const api = window.netflix?.appContext?.state?.playerApp?.getAPI?.();
    const videoPlayer = api?.videoPlayer;
    if (!videoPlayer) return null;

    const sessionIds: string[] = videoPlayer.getAllPlayerSessionIds?.() ?? [];
    const sessionId =
      sessionIds.find((id) => String(id).startsWith('watch-')) ?? sessionIds[0];
    if (!sessionId) return null;

    const player = videoPlayer.getVideoPlayerBySessionId(sessionId);
    if (!player) return null;

    const currentTime =
      typeof player.getCurrentTime === 'function' ? player.getCurrentTime() : null;
    const duration =
      typeof player.getDuration === 'function' ? player.getDuration() : null;

    let metadata: any = null;
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
      currentTime: Number.isFinite(currentTime) ? currentTime : null,
      duration: Number.isFinite(duration) && duration > 0 ? duration : null,
      metadata,
    };
  } catch {
    return null;
  }
}

window.addEventListener('message', (event) => {
  if (event.source !== window) return;

  if (event.data?.type === 'GET_NETFLIX_DATA') {
    try {
      const url = window.location.href;
      const videoIdMatch = url.match(/\/watch\/(\d+)/);

      let responseData: any = null;

      if (videoIdMatch && videoIdMatch[1]) {
        const videoId = videoIdMatch[1];
        // @ts-ignore
        const videoData = window.netflix?.falcorCache?.videos?.[videoId];

        if (videoData?.summary?.value) {
          responseData = {
            videoId: videoId,
            summary: videoData.summary.value,
            runtime: videoData.runtime?.value || null,
            creditsOffset: videoData.creditsOffset?.value || null,
          };
        }
      }

      const player = getPlayerState();
      if (player) {
        responseData = { ...(responseData ?? {}), player };
      }

      window.postMessage(
        {
          type: 'NETFLIX_DATA_RESPONSE',
          requestId: event.data.requestId,
          success: !!responseData,
          data: responseData,
        },
        '*',
      );
    } catch (error) {
      window.postMessage(
        {
          type: 'NETFLIX_DATA_RESPONSE',
          requestId: event.data.requestId,
          success: false,
          error: error instanceof Error ? error.message : 'Unknown error',
        },
        '*',
      );
    }
  }
});
