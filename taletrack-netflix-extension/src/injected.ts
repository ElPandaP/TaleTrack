// Runs in the page (MAIN) context
// Has access to window.netflix

console.log('Netflix data extractor injected in MAIN world');

window.addEventListener('message', (event) => {
  if (event.source !== window) return;

  if (event.data?.type === 'GET_NETFLIX_DATA') {
    try {
      const url = window.location.href;
      const videoIdMatch = url.match(/\/watch\/(\d+)/);

      let responseData = null;

      if (videoIdMatch && videoIdMatch[1]) {
        const videoId = videoIdMatch[1];
        // @ts-ignore
        const videoData = window.netflix?.falcorCache?.videos?.[videoId];

        if (videoData?.summary?.value) {
          responseData = {
            videoId: videoId,
            summary: videoData.summary.value,
            runtime: videoData.runtime?.value || null,
            creditsOffset: videoData.creditsOffset?.value || null
          };
        }
      }

      window.postMessage({
        type: 'NETFLIX_DATA_RESPONSE',
        requestId: event.data.requestId,
        success: !!responseData,
        data: responseData
      }, '*');
    } catch (error) {
      window.postMessage({
        type: 'NETFLIX_DATA_RESPONSE',
        requestId: event.data.requestId,
        success: false,
        error: error instanceof Error ? error.message : 'Unknown error'
      }, '*');
    }
  }
});