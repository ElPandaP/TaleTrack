import type { NetflixMedia, ExtractDataMessage, ExtractDataResponse } from './types';

// Cache for data from the page
let netflixData: any = null;

const PROGRESS_THRESHOLD = 0.8;
const PROGRESS_POLL_MS = 10000;
const reportedVideos = new Set<string>();

chrome.runtime.onMessage.addListener((message: ExtractDataMessage, sender, sendResponse) => {
  if (message.action === 'extractData') {
    // Ask the page context for Netflix data
    requestNetflixData().then(() => {
      const data = extractNetflixData();

      if (data) {
        sendResponse({ success: true, data });
      } else {
        sendResponse({ success: false, error: 'No se pudieron extraer los datos' });
      }
    });

    return true; // Keep the channel open
  }

  return true;
});

// Pull data from the main page context
async function requestNetflixData(): Promise<void> {
  return new Promise((resolve) => {
    // Unique request id
    const requestId = `netflix-data-${Date.now()}`;

    // Listen for the response
    const messageHandler = (event: MessageEvent) => {
      if (event.source !== window) {
        return;
      }
      if (event.data?.type === 'NETFLIX_DATA_RESPONSE' && event.data?.requestId === requestId) {
        netflixData = event.data.data;
        if (netflixData) {
          console.log('Received Netflix data:', netflixData);
        }
        window.removeEventListener('message', messageHandler);
        resolve();
      }
    };

    window.addEventListener('message', messageHandler);

    // Ask the page for data
    window.postMessage({
      type: 'GET_NETFLIX_DATA',
      requestId: requestId
    }, '*');

    // Timeout after 1 second
    setTimeout(() => {
      window.removeEventListener('message', messageHandler);
      resolve();
    }, 1000);
  });
}

function extractNetflixData(): NetflixMedia | null {
  try {
    console.log('URL:', window.location.href);

    const rawTitle = extractTitle() || 'Título no encontrado';
    console.log('Raw title:', rawTitle);

    const { season, episode } = extractSeasonEpisode(rawTitle);
    console.log('Season:', season, 'Episode:', episode);

    const type = extractType(season, episode);
    console.log('Type:', type);

    const { title, episodeTitle } = cleanTitle(rawTitle);
    console.log('Clean title:', title, 'Episode title:', episodeTitle);

    const year = extractYear();
    const genres = extractGenres();
    const duration = extractDuration();
    const description = extractDescription();
    const imageUrl = extractImageUrl();

    const media: NetflixMedia = {
      title,
      type,
      genres,
      netflixUrl: window.location.href,
      extractedAt: new Date().toISOString()
    };

    if (year) {
      media.year = year;
    }
    if (duration) {
      media.duration = duration;
    }
    if (description) {
      media.description = description;
    }
    if (imageUrl) {
      media.imageUrl = imageUrl;
    }

    if (type === 'series') {
      if (season) {
        media.season = season;
      }
      if (episode) {
        media.episode = episode;
      }
      if (episodeTitle) {
        media.episodeTitle = episodeTitle;
      }
    }

    return media;
  } catch (error) {
    console.error('Error extrayendo datos:', error);
    return null;
  }
}

function extractSeasonEpisode(rawTitle: string): { season: number | null; episode: number | null } {
  const url = window.location.href;

  // Use data from the injected script first
  if (netflixData?.summary) {
    const summary = netflixData.summary;
    console.log('Netflix data from MAIN world:', summary);

    if (summary.type === 'episode' && summary.season && summary.episode) {
      return {
        season: summary.season,
        episode: summary.episode
      };
    }
  }

  // Check query parameters
  let seasonMatch = url.match(/[?&]season=(\d+)/);
  let episodeMatch = url.match(/[?&]episode=(\d+)/);

  if (seasonMatch && seasonMatch[1] && episodeMatch && episodeMatch[1]) {
    return {
      season: parseInt(seasonMatch[1], 10),
      episode: parseInt(episodeMatch[1], 10)
    };
  }

  // Check hash fragments
  seasonMatch = url.match(/#.*season=(\d+)/);
  episodeMatch = url.match(/#.*episode=(\d+)/);

  if (seasonMatch && seasonMatch[1] && episodeMatch && episodeMatch[1]) {
    return {
      season: parseInt(seasonMatch[1], 10),
      episode: parseInt(episodeMatch[1], 10)
    };
  }

  // Parse title text
  const titlePattern = /T(\d+):? E(\d+)|E(\d+)/i;
  const titleMatch = rawTitle.match(titlePattern);

  if (titleMatch) {
    if (titleMatch[1] && titleMatch[2]) {
      return {
        season: parseInt(titleMatch[1], 10),
        episode: parseInt(titleMatch[2], 10)
      };
    }

    if (titleMatch[3]) {
      return {
        season: null,
        episode: parseInt(titleMatch[3], 10)
      };
    }
  }

  // Look in the DOM
  const episodeInfo = document.querySelector('[data-uia="video-title"]')?.textContent;
  if (episodeInfo) {
    const match = episodeInfo.match(/T(\d+):?\s*E(\d+)/i);
    if (match && match[1] && match[2]) {
      return {
        season: parseInt(match[1], 10),
        episode: parseInt(match[2], 10)
      };
    }
  }

  return {
    season: null,
    episode: null
  };
}

function cleanTitle(rawTitle: string): { title: string; episodeTitle: string | null } {
  const pattern1 = /^(.+?)(?:T(\d+):)?E(\d+)(.*)$/i;
  const match1 = rawTitle.match(pattern1);

  if (match1 && match1[1]) {
    return {
      title: match1[1].trim(),
      episodeTitle: match1[4] ? match1[4].trim() : null
    };
  }

  const pattern2 = /^(.+?):\s*(.+)$/;
  const match2 = rawTitle.match(pattern2);

  if (match2 && match2[1] && match2[2]) {
    if (!match2[2].match(/^(La |El |Una |Un )/i)) {
      return {
        title: match2[1].trim(),
        episodeTitle: match2[2].trim()
      };
    }
  }

  const pattern3 = /^(.+? )\s*[-–]\s*(.+)$/;
  const match3 = rawTitle.match(pattern3);

  if (match3 && match3[1] && match3[2]) {
    return {
      title: match3[1].trim(),
      episodeTitle: match3[2].trim()
    };
  }

  return {
    title: rawTitle.trim(),
    episodeTitle: null
  };
}

function extractTitle(): string | null {
  const selectors = [
    '.title-title',
    'h1.title-title',
    '[data-uia="video-title"]',
    '[data-uia="title-name"]',
    'h1[class*="title"]',
    '.ellipsize-text h4',
    '.video-title'
  ];

  for (const selector of selectors) {
    const element = document.querySelector(selector);
    if (element?.textContent?.trim()) {
      return element.textContent.trim();
    }
  }

  const pageTitle = document.title;
  if (pageTitle && pageTitle !== 'Netflix') {
    const match = pageTitle.match(/^(.+? )\s*[-–|]\s*Netflix/);
    if (match && match[1]) {
      return match[1].trim();
    }
  }

  const jsonLd = document.querySelector('script[type="application/ld+json"]');
  if (jsonLd?.textContent) {
    try {
      const data = JSON.parse(jsonLd.textContent);
      if (data.name) return data.name;
      if (data['@graph']?.[0]?.name) return data['@graph'][0].name;
    } catch (e) {
      // Ignore
    }
  }

  return null;
}

function extractYear(): number | null {
  const selectors = [
    '.title-info-metadata-item:first-child',
    '[data-uia="title-year"]',
    '.year',
    '.item-year'
  ];

  for (const selector of selectors) {
    const element = document.querySelector(selector);
    const text = element?.textContent?.trim();
    if (text) {
      const match = text.match(/(\d{4})/);
      if (match && match[1]) {
        return parseInt(match[1], 10);
      }
    }
  }

  return null;
}

function extractType(season: number | null, episode: number | null): 'movie' | 'series' {
  if (season !== null || episode !== null) {
    console.log('Método 1');
    return 'series';
  }

  const url = window.location.href;

  if (url.match(/[?&#]season=/) || url.match(/[?&#]episode=/)) {
    console.log('Método 2');
    return 'series';
  }

  const seriesIndicators = [
    '.episodes-container',
    '[data-uia="season-selector"]',
    '.season-selector',
    '.episode-selector',
    '[class*="episode"]',
    '[class*="season"]'
  ];

  for (const selector of seriesIndicators) {
    if (document.querySelector(selector)) {
      console.log('Método 3');
      return 'series';
    }
  }

  const titleText = document.querySelector('[data-uia="video-title"]')?.textContent;
  if (titleText?.match(/T\d+:?\s*E\d+/i)) {
    console.log('Método 4');
    return 'series';
  }

  if (url.includes('/watch/')) {
    const pageTitle = document.title;
    if (pageTitle.match(/T\d+|E\d+|Temporada|Episodio/i)) {
      console.log('Método 5');
      console.log("");
      return 'series';
    }
  }

  return 'movie';
}

function extractGenres(): string[] {
  const genres: string[] = [];

  const selectors = [
    '.item-genres',
    '[data-uia="item-genres"]',
    '.genre',
    '.title-info-metadata .item-genre'
  ];

  for (const selector of selectors) {
    const elements = document.querySelectorAll(selector);
    elements.forEach(el => {
      const text = el.textContent?.trim();
      if (text) {
        genres.push(...text.split(/[,•·]/).map(g => g.trim()).filter(Boolean));
      }
    });

    if (genres.length > 0) {
      break;
    }
  }

  return [...new Set(genres)];
}

function extractDuration(): string | null {
  const selectors = [
    '.duration',
    '[data-uia="item-duration"]',
    '.title-info-metadata .runtime',
    '.item-runtime'
  ];

  for (const selector of selectors) {
    const element = document.querySelector(selector);
    const text = element?.textContent?.trim();
    if (text && (text.includes('min') || text.includes('h') || text.includes('temporada'))) {
      return text;
    }
  }

  return null;
}

function extractDescription(): string | null {
  const selectors = [
    '.title-info-synopsis',
    '[data-uia="title-description"]',
    '.previewModal--info-synopsis',
    'div.ptrack-content p',
    '.synopsis'
  ];

  for (const selector of selectors) {
    const element = document.querySelector(selector);
    if (element?.textContent?.trim()) {
      return element.textContent.trim();
    }
  }

  return null;
}

function extractImageUrl(): string | null {
  const selectors = [
    '.title-logo img',
    '.previewModal--player-titleTreatment-logo img',
    'img[data-uia="title-image"]',
    '.boxart-image'
  ];

  for (const selector of selectors) {
    const img = document.querySelector(selector) as HTMLImageElement;
    if (img?.src) {
      return img.src;
    }
  }

  return null;
}

function getCurrentVideoId(): string | null {
  const match = window.location.href.match(/\/watch\/(\d+)/);
  return match && match[1] ? match[1] : null;
}

async function sendViewed(media: NetflixMedia, progress: number): Promise<void> {
  console.log('View threshold reached:', { ...media, progress, reportedAt: new Date().toISOString() });
}

async function checkProgressAndReport(): Promise<void> {
  const video = document.querySelector('video') as HTMLVideoElement | null;
  if (!video || !video.duration || !Number.isFinite(video.duration)) {
    console.error('No video found');
    return;
  }

  const progress = video.currentTime / video.duration;
  console.log('Progress check:', { current: progress, target: PROGRESS_THRESHOLD });
  if (progress < PROGRESS_THRESHOLD) {
    return;
  }

  const videoId = getCurrentVideoId() || window.location.href;
  if (reportedVideos.has(videoId)) {
    return;
  }
  reportedVideos.add(videoId);

  await requestNetflixData();
  const data = extractNetflixData();
  if (data) {
    await sendViewed(data, progress);
  }
}

function startProgressWatcher(): void {
  setInterval(() => {
    checkProgressAndReport().catch(err => console.error('Fallo en checkProgressAndReport:', err));
  }, PROGRESS_POLL_MS);
}

if (window.location.href.includes('/watch/')) {
  startProgressWatcher();
  console.log('Netflix Tracker progress watcher started');
} else {
  console.log('Netflix Tracker content script loaded (not on watch page)');
}