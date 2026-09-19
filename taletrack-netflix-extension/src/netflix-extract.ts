// Reads a Netflix watch/browse page (DOM + the MAIN-world player API) and
// turns it into a NetflixMedia. content.ts calls extractNetflixData() and
// only deals with when to call it and what to do with the result.

import type { NetflixMedia } from './types';
import { AUTO_POLL_MS } from './config';

// Data pulled from the page's MAIN world via requestNetflixData().
let netflixData: any = null;

// Placeholder title when nothing — metadata, DOM, cache — has one yet.
export const NO_TITLE = 'Título no encontrado';

// The on-screen title bar is the only real source for the title (Netflix's
// player API doesn't expose it), and it's only visible for a few seconds.
// Cache the last one seen per video id so it survives after the bar fades.
const titleCache = new Map<string, string>();

// Margin before the credits (seconds) that counts as "close enough, call it
// done". At least one poll interval, so a single check can't miss the window
// entirely; doubled for slack.
export const CREDITS_MARGIN_SECONDS = (AUTO_POLL_MS / 1000) * 2;

/** Where the credits start (seconds), from the last data read via requestNetflixData(). */
export function getCreditsOffsetSeconds(): number | null {
  const value = netflixData?.creditsOffset;
  return typeof value === 'number' && value > 0 ? value : null;
}

/** Ask the page (MAIN world) for its Netflix player/Falcor data and cache it. */
export async function requestNetflixData(): Promise<void> {
  return new Promise((resolve) => {
    const requestId = `netflix-data-${Date.now()}`;

    const messageHandler = (event: MessageEvent) => {
      if (event.source !== window) {
        return;
      }
      if (event.data?.type === 'NETFLIX_DATA_RESPONSE' && event.data?.requestId === requestId) {
        netflixData = event.data.data;
        window.removeEventListener('message', messageHandler);
        resolve();
      }
    };

    window.addEventListener('message', messageHandler);

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

/** "1h 47min" / "48min" from a total number of seconds. */
function formatRuntime(totalSeconds: number): string {
  const s = Math.max(0, Math.round(totalSeconds));
  const h = Math.floor(s / 3600);
  const m = Math.round((s % 3600) / 60);
  return h > 0 ? `${h}h ${m}min` : `${m}min`;
}

// TODO(depuración temporal): comprobar si el elemento <video> del DOM basta
// por sí solo (posición, duración) sin depender de la API interna de Netflix
// (netflixData.player, vía injected.ts). Los console.log de abajo comparan
// ambas fuentes en paralelo. Si tras unas cuantas reproducciones el <video>
// del DOM siempre da los mismos valores que la API del reproductor, quitar
// la dependencia de la API interna (injected.ts, requestNetflixData) y
// quedarse solo con el DOM. Quitar también estos logs al cerrar esto.

/** Current playback position + total runtime + progress %, when watching. */
function extractPlayback(): { progressPercent?: number; positionSeconds?: number; runtimeSeconds?: number } | null {
  let positionMs: number | undefined;
  let durationMs: number | undefined;

  // 1. Netflix player API (from the MAIN-world injected script) — most accurate
  const player = netflixData?.player;
  if (player) {
    if (Number.isFinite(player.currentTime)) positionMs = player.currentTime;
    if (Number.isFinite(player.duration) && player.duration > 0) durationMs = player.duration;
  }

  // 2. The <video> element (content scripts share the page DOM)
  const video = document.querySelector('video') as HTMLVideoElement | null;
  console.log('[TaleTrack][depuración] API interna del reproductor ->', player
    ? { currentTime: player.currentTime, duration: player.duration }
    : 'no disponible');
  console.log('[TaleTrack][depuración] elemento <video> del DOM ->', video
    ? { currentTime: video.currentTime, duration: video.duration }
    : 'no encontrado');
  if (video) {
    if (positionMs === undefined && Number.isFinite(video.currentTime)) {
      positionMs = video.currentTime * 1000;
    }
    if (durationMs === undefined && Number.isFinite(video.duration) && video.duration > 0) {
      durationMs = video.duration * 1000;
    }
  }

  // 3. Falcor runtime (seconds) — total length only, last resort
  if (durationMs === undefined && Number.isFinite(netflixData?.runtime)) {
    durationMs = netflixData.runtime * 1000;
  }

  if (positionMs === undefined && durationMs === undefined) return null;

  const result: { progressPercent?: number; positionSeconds?: number; runtimeSeconds?: number } = {};
  if (positionMs !== undefined) result.positionSeconds = Math.round(positionMs / 1000);
  if (durationMs !== undefined) result.runtimeSeconds = Math.round(durationMs / 1000);
  if (positionMs !== undefined && durationMs !== undefined && durationMs > 0) {
    result.progressPercent = Math.max(0, Math.min(100, Math.round((positionMs / durationMs) * 100)));
  }

  // Netflix tells us where the credits start (seconds). Once you're within
  // this margin of them — or already past — assume you'll finish and count
  // it as done, instead of tracking stalling just under 100%.
  const creditsOffsetSeconds = netflixData?.creditsOffset;
  if (
    result.positionSeconds !== undefined &&
    typeof creditsOffsetSeconds === 'number' &&
    creditsOffsetSeconds > 0 &&
    result.positionSeconds >= creditsOffsetSeconds - CREDITS_MARGIN_SECONDS
  ) {
    result.progressPercent = 100;
  }

  return result;
}

/** The player's raw video metadata — its shape varies, hence the two possible paths. */
function getMetaVideo(): any {
  return netflixData?.player?.metadata?.video ?? netflixData?.player?.metadata?._metadata?.video;
}

/** Find season/episode sequence numbers in the Netflix player metadata. */
function seasonEpisodeFromMetadata(): { season: number | null; episode: number | null } | null {
  const video = getMetaVideo();
  if (!video || !Array.isArray(video.seasons)) return null;

  const currentId = video.currentEpisode ?? video.episodeId;
  if (!currentId) return null;

  for (const season of video.seasons) {
    const ep = (season.episodes ?? []).find((e: any) => e.id === currentId);
    if (ep) {
      return {
        season: season.seq ?? season.season ?? null,
        episode: ep.seq ?? ep.episode ?? null,
      };
    }
  }
  return null;
}

function extractSeasonEpisode(rawTitle: string): { season: number | null; episode: number | null } {
  const url = window.location.href;

  // Netflix player metadata is the most reliable source while watching
  const fromMeta = seasonEpisodeFromMetadata();
  if (fromMeta && (fromMeta.season !== null || fromMeta.episode !== null)) {
    return { season: fromMeta.season, episode: fromMeta.episode };
  }

  // Use data from the injected script first
  if (netflixData?.summary) {
    const summary = netflixData.summary;

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

/** Strips episode-number/subtitle noise off a scraped title, keeping just the show/movie name. */
function cleanTitle(rawTitle: string): string {
  const pattern1 = /^(.+?)(?:T(\d+):)?E(\d+)(.*)$/i;
  const match1 = rawTitle.match(pattern1);
  if (match1 && match1[1]) return match1[1].trim();

  const pattern2 = /^(.+?):\s*(.+)$/;
  const match2 = rawTitle.match(pattern2);
  if (match2 && match2[1] && match2[2] && !match2[2].match(/^(La |El |Una |Un )/i)) {
    return match2[1].trim();
  }

  const pattern3 = /^(.+? )\s*[-–]\s*(.+)$/;
  const match3 = rawTitle.match(pattern3);
  if (match3 && match3[1] && match3[2]) return match3[1].trim();

  return rawTitle.trim();
}

function extractTitle(): string | null {
  console.log('[TaleTrack] extractTitle: trying player metadata (getMetaVideo)…');
  // Most reliable: Netflix's own player metadata / Falcor summary — available for
  // the whole session, unlike the on-screen title bar (which only the DOM
  // selectors below can see, and which fades out a few seconds into playback).
  const metaTitle = getMetaVideo()?.title;
  if (typeof metaTitle === 'string' && metaTitle.trim()) {
    console.log('[TaleTrack] extractTitle: found via player metadata ->', metaTitle);
    return metaTitle.trim();
  }
  console.log('[TaleTrack] extractTitle: not in player metadata (value was', metaTitle, ')');

  console.log('[TaleTrack] extractTitle: trying Falcor summary.title…');
  const summaryTitle = netflixData?.summary?.title;
  if (typeof summaryTitle === 'string' && summaryTitle.trim()) {
    console.log('[TaleTrack] extractTitle: found via summary.title ->', summaryTitle);
    return summaryTitle.trim();
  }
  console.log('[TaleTrack] extractTitle: not in summary.title (value was', summaryTitle, ')');

  const selectors = [
    '.title-title',
    'h1.title-title',
    '[data-uia="video-title"]',
    '[data-uia="title-name"]',
    'h1[class*="title"]',
    '.ellipsize-text h4',
    '.video-title'
  ];

  console.log('[TaleTrack] extractTitle: trying DOM selectors…', selectors);
  for (const selector of selectors) {
    const element = document.querySelector(selector);
    if (element?.textContent?.trim()) {
      const found = element.textContent.trim();
      console.log(`[TaleTrack] extractTitle: found via DOM selector "${selector}" ->`, found);
      if (netflixData?.videoId) titleCache.set(netflixData.videoId, found);
      return found;
    }
  }
  console.log('[TaleTrack] extractTitle: no DOM selector matched (title bar probably faded out)');

  // The bar's gone, but we may have caught it earlier this session for this
  // exact video (e.g. right when playback started).
  const cached = netflixData?.videoId ? titleCache.get(netflixData.videoId) : undefined;
  if (cached) {
    console.log('[TaleTrack] extractTitle: found in cache for videoId', netflixData?.videoId, '->', cached);
    return cached;
  }
  console.log('[TaleTrack] extractTitle: nothing cached for videoId', netflixData?.videoId);

  const pageTitle = document.title;
  console.log('[TaleTrack] extractTitle: trying document.title ->', pageTitle);
  if (pageTitle && pageTitle !== 'Netflix') {
    const match = pageTitle.match(/^(.+? )\s*[-–|]\s*Netflix/);
    if (match && match[1]) {
      console.log('[TaleTrack] extractTitle: found via document.title ->', match[1].trim());
      return match[1].trim();
    }
  }

  const jsonLd = document.querySelector('script[type="application/ld+json"]');
  if (jsonLd?.textContent) {
    try {
      const data = JSON.parse(jsonLd.textContent);
      if (data.name) {
        console.log('[TaleTrack] extractTitle: found via JSON-LD ->', data.name);
        return data.name;
      }
      if (data['@graph']?.[0]?.name) {
        console.log('[TaleTrack] extractTitle: found via JSON-LD @graph ->', data['@graph'][0].name);
        return data['@graph'][0].name;
      }
    } catch {
      /* malformed JSON-LD */
    }
  }

  console.log('[TaleTrack] extractTitle: gave up, no source had a title');
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

/**
 * Movie vs series, tried in order of reliability: parsed season/episode numbers,
 * player metadata, URL params, series-only DOM elements, then title text patterns.
 */
function extractType(season: number | null, episode: number | null): 'movie' | 'series' {
  if (season !== null || episode !== null) return 'series';

  // Netflix player metadata is explicit about show vs movie
  const metaVideo = getMetaVideo();
  if (metaVideo?.type === 'show' || Array.isArray(metaVideo?.seasons)) return 'series';
  if (metaVideo?.type === 'movie') return 'movie';

  const url = window.location.href;
  if (url.match(/[?&#]season=/) || url.match(/[?&#]episode=/)) return 'series';

  const seriesIndicators = [
    '.episodes-container',
    '[data-uia="season-selector"]',
    '.season-selector',
    '.episode-selector',
    '[class*="episode"]',
    '[class*="season"]'
  ];
  if (seriesIndicators.some((selector) => document.querySelector(selector))) return 'series';

  const titleText = document.querySelector('[data-uia="video-title"]')?.textContent;
  if (titleText?.match(/T\d+:?\s*E\d+/i)) return 'series';

  if (url.includes('/watch/')) {
    const pageTitle = document.title;
    if (pageTitle.match(/T\d+|E\d+|Temporada|Episodio/i)) return 'series';
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

/**
 * Netflix UI language, e.g. "es" or "en" — read from <html lang="es-ES">.
 * Only "es"/"en" are recognised server-side for TMDB enrichment; anything
 * else is sent as-is and simply ignored there.
 */
function extractLanguage(): string | null {
  const lang = document.documentElement.lang;
  if (!lang) return null;
  return (lang.split('-')[0] ?? lang).toLowerCase();
}

export function extractNetflixData(): NetflixMedia | null {
  try {
    console.log('[TaleTrack] extractNetflixData: intentando extraer título…');
    const rawTitle = extractTitle() || NO_TITLE;
    console.log('[TaleTrack] extractNetflixData: rawTitle ->', rawTitle);
    const { season, episode } = extractSeasonEpisode(rawTitle);
    const type = extractType(season, episode);
    const title = cleanTitle(rawTitle);
    const playback = extractPlayback();
    const year = extractYear();
    const genres = extractGenres();
    const duration = extractDuration();
    const description = extractDescription();
    const language = extractLanguage();

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
    if (description) {
      media.description = description;
    }
    if (language) {
      media.language = language;
    }

    // Playback: progress %, current position, total runtime
    if (playback) {
      if (playback.progressPercent !== undefined) {
        media.progressPercent = playback.progressPercent;
      }
      if (playback.positionSeconds !== undefined) {
        media.positionSeconds = playback.positionSeconds;
      }
      if (playback.runtimeSeconds !== undefined) {
        media.runtimeSeconds = playback.runtimeSeconds;
      }
    }

    // Prefer the real runtime for the human-readable duration; fall back to the
    // DOM string (only present on browse/title pages, not while watching).
    if (media.runtimeSeconds !== undefined) {
      media.duration = formatRuntime(media.runtimeSeconds);
    } else if (duration) {
      media.duration = duration;
    }

    if (type === 'series') {
      if (season) {
        media.season = season;
      }
      if (episode) {
        media.episode = episode;
      }
    }

    return media;
  } catch (error) {
    console.error('Error extrayendo datos:', error);
    return null;
  }
}
