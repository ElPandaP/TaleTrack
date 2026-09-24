// Reads a Netflix watch/browse page (DOM + the MAIN-world player API) and
// turns it into a NetflixMedia. content.ts calls extractNetflixData() and
// only deals with when to call it and what to do with the result.

import type { NetflixMedia, NetflixPageData, NetflixVideoMetadata } from './types';
import { AUTO_POLL_MS, debug } from './config';

type SeasonEpisode = { season: number | null; episode: number | null };

// Data pulled from the page's MAIN world via requestNetflixData().
let netflixData: NetflixPageData | null = null;

// The on-screen title bar is a real source for the title (Netflix's player API
// doesn't always expose it), and it's only visible for a few seconds. Cache the
// last one seen per watch id so it survives after the bar fades.
const titleCache = new Map<string, string>();

// Margin before the credits (seconds) that counts as "close enough, call it
// done". At least one poll interval, so a single check can't miss the window
// entirely; doubled for slack.
export const CREDITS_MARGIN_SECONDS = (AUTO_POLL_MS / 1000) * 2;

/** The id in the current /watch/<id> URL, or null when not on a player page. */
export function getWatchVideoId(): string | null {
  return window.location.href.match(/\/watch\/(\d+)/)?.[1] ?? null;
}

/** Where the credits start (seconds), from the last data read via requestNetflixData(). */
export function getCreditsOffsetSeconds(): number | null {
  const value = netflixData?.creditsOffset;
  return typeof value === 'number' && value > 0 ? value : null;
}

/** Ask the page (MAIN world) for its Netflix player/Falcor data and cache it. */
export function requestNetflixData(): Promise<void> {
  return new Promise((resolve) => {
    const requestId = `netflix-data-${crypto.randomUUID()}`;
    let timer: ReturnType<typeof setTimeout>;

    const finish = () => {
      clearTimeout(timer);
      window.removeEventListener('message', onMessage);
      resolve();
    };

    const onMessage = (event: MessageEvent) => {
      if (event.source !== window) return;
      if (event.data?.type === 'NETFLIX_DATA_RESPONSE' && event.data?.requestId === requestId) {
        netflixData = (event.data.data as NetflixPageData | null) ?? null;
        finish();
      }
    };

    window.addEventListener('message', onMessage);
    window.postMessage({ type: 'GET_NETFLIX_DATA', requestId }, window.location.origin);

    // Give up after 1 second.
    timer = setTimeout(finish, 1000);
  });
}

/** Total runtime of the movie / episode in seconds, when watching. */
function extractRuntimeSeconds(): number | undefined {
  let durationMs: number | undefined;

  // 1. Netflix player API (from the MAIN-world injected script) — most accurate
  const playerDuration = netflixData?.player?.duration;
  if (typeof playerDuration === 'number' && Number.isFinite(playerDuration) && playerDuration > 0) {
    durationMs = playerDuration;
  }

  // 2. The <video> element (content scripts share the page DOM)
  const video = document.querySelector('video');
  if (durationMs === undefined && video && Number.isFinite(video.duration) && video.duration > 0) {
    durationMs = video.duration * 1000;
  }

  // 3. Falcor runtime (seconds) — last resort
  const falcorRuntime = netflixData?.runtime;
  if (durationMs === undefined && typeof falcorRuntime === 'number' && Number.isFinite(falcorRuntime)) {
    durationMs = falcorRuntime * 1000;
  }

  return durationMs === undefined ? undefined : Math.round(durationMs / 1000);
}

/** The player's raw video metadata — its shape varies, hence the two possible paths. */
function getMetaVideo(): NetflixVideoMetadata | undefined {
  const metadata = netflixData?.player?.metadata;
  return metadata?.video ?? metadata?._metadata?.video;
}

/** Find season/episode sequence numbers in the Netflix player metadata. */
function seasonEpisodeFromMetadata(): SeasonEpisode | null {
  const video = getMetaVideo();
  if (!video || !Array.isArray(video.seasons)) return null;

  const currentId = video.currentEpisode ?? video.episodeId;
  if (!currentId) return null;

  for (const season of video.seasons) {
    const ep = (season.episodes ?? []).find((e) => e.id === currentId);
    if (ep) {
      return {
        season: season.seq ?? season.season ?? null,
        episode: ep.seq ?? ep.episode ?? null,
      };
    }
  }
  return null;
}

function seasonEpisodeFromUrl(url: string): SeasonEpisode | null {
  const forms: [RegExp, RegExp][] = [
    [/[?&]season=(\d+)/, /[?&]episode=(\d+)/], // query parameters
    [/#.*season=(\d+)/, /#.*episode=(\d+)/], // hash fragment
  ];
  for (const [seasonPattern, episodePattern] of forms) {
    const season = url.match(seasonPattern)?.[1];
    const episode = url.match(episodePattern)?.[1];
    if (season && episode) return { season: parseInt(season, 10), episode: parseInt(episode, 10) };
  }
  return null;
}

/**
 * Season/episode numbers from marker text such as "T1:E2", "S1:E2" or "E2". Case-sensitive and
 * word-bounded, so a title like "Se7en" is not read as an episode.
 */
export function parseEpisodeMarker(text: string): SeasonEpisode | null {
  const full = text.match(/\b[TS](\d+):?\s*E(\d+)\b/);
  if (full?.[1] && full[2]) return { season: parseInt(full[1], 10), episode: parseInt(full[2], 10) };

  const bare = text.match(/\bE(\d+)\b/);
  if (bare?.[1]) return { season: null, episode: parseInt(bare[1], 10) };

  return null;
}

function extractSeasonEpisode(rawTitle: string): SeasonEpisode {
  // Netflix player metadata is the most reliable source while watching
  const fromMeta = seasonEpisodeFromMetadata();
  if (fromMeta && (fromMeta.season !== null || fromMeta.episode !== null)) return fromMeta;

  const summary = netflixData?.summary;
  if (summary?.type === 'episode' && summary.season && summary.episode) {
    return { season: summary.season, episode: summary.episode };
  }

  const fromUrl = seasonEpisodeFromUrl(window.location.href);
  if (fromUrl) return fromUrl;

  const fromTitle = parseEpisodeMarker(rawTitle);
  if (fromTitle) return fromTitle;

  // The video-title element runs the show name and the marker together, so no word boundary here.
  const episodeInfo = document.querySelector('[data-uia="video-title"]')?.textContent;
  const match = episodeInfo?.match(/[TS](\d+):?\s*E(\d+)/);
  if (match?.[1] && match[2]) return { season: parseInt(match[1], 10), episode: parseInt(match[2], 10) };

  return { season: null, episode: null };
}

/**
 * Strips episode-number/subtitle noise off a title scraped from the page, keeping just the show
 * name. Only for series: a movie's own ":" or "-" is part of its name, and titles that come from
 * Netflix's metadata are already bare.
 */
export function cleanTitle(rawTitle: string): string {
  // "Show T1:E2 Episode name", or the same run together as "ShowT1:E2Episode name"
  const marked = rawTitle.match(/^(.+?)(?:[TS]\d+:)?E\d+/);
  if (marked?.[1]) return marked[1].trim();

  // "Show: Episode name", unless what follows reads as part of the name itself
  const colon = rawTitle.match(/^(.+?):\s*(.+)$/);
  if (colon?.[1] && colon[2] && !/^(La |El |Una |Un |The |A |An )/i.test(colon[2])) return colon[1].trim();

  // "Show - Episode name"
  const dash = rawTitle.match(/^(.+? )\s*[-–]\s*(.+)$/);
  if (dash?.[1] && dash[2]) return dash[1].trim();

  return rawTitle.trim();
}

/**
 * The title and whether it is already bare (straight from Netflix's metadata) or scraped text that
 * may still carry episode noise. Null when no source has one yet.
 */
function extractTitle(): { text: string; bare: boolean } | null {
  // Most reliable: Netflix's own player metadata / Falcor summary — available for
  // the whole session, unlike the on-screen title bar (which only the DOM
  // selectors below can see, and which fades out a few seconds into playback).
  const metaTitle = getMetaVideo()?.title;
  if (typeof metaTitle === 'string' && metaTitle.trim()) {
    debug('title from player metadata:', metaTitle);
    return { text: metaTitle.trim(), bare: true };
  }

  const summaryTitle = netflixData?.summary?.title;
  if (typeof summaryTitle === 'string' && summaryTitle.trim()) {
    debug('title from Falcor summary:', summaryTitle);
    return { text: summaryTitle.trim(), bare: true };
  }

  const watchId = getWatchVideoId();
  const selectors = [
    '.title-title',
    'h1.title-title',
    '[data-uia="video-title"]',
    '[data-uia="title-name"]',
    'h1[class*="title"]',
    '.ellipsize-text h4',
    '.video-title',
  ];
  for (const selector of selectors) {
    const found = document.querySelector(selector)?.textContent?.trim();
    if (found) {
      debug(`title from DOM selector "${selector}":`, found);
      if (watchId) titleCache.set(watchId, found);
      return { text: found, bare: false };
    }
  }

  // The bar's gone, but we may have caught it earlier this session for this
  // exact video (e.g. right when playback started).
  const cached = watchId ? titleCache.get(watchId) : undefined;
  if (cached) {
    debug('title from cache:', cached);
    return { text: cached, bare: false };
  }

  const pageTitle = document.title;
  if (pageTitle && pageTitle !== 'Netflix') {
    const match = pageTitle.match(/^(.+? )\s*[-–|]\s*Netflix/);
    if (match?.[1]) {
      debug('title from document.title:', match[1].trim());
      return { text: match[1].trim(), bare: false };
    }
  }

  const jsonLd = document.querySelector('script[type="application/ld+json"]');
  if (jsonLd?.textContent) {
    try {
      const data: { name?: unknown; '@graph'?: { name?: unknown }[] } = JSON.parse(jsonLd.textContent);
      const name = data.name ?? data['@graph']?.[0]?.name;
      if (typeof name === 'string' && name.trim()) {
        debug('title from JSON-LD:', name);
        return { text: name.trim(), bare: false };
      }
    } catch {
      /* malformed JSON-LD */
    }
  }

  debug('no source had a title yet');
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
  if (/[?&#]season=/.test(url) || /[?&#]episode=/.test(url)) return 'series';

  const seriesIndicators = [
    '.episodes-container',
    '[data-uia="season-selector"]',
    '.season-selector',
    '.episode-selector',
  ];
  if (seriesIndicators.some((selector) => document.querySelector(selector))) return 'series';

  const titleText = document.querySelector('[data-uia="video-title"]')?.textContent;
  if (titleText && /[TS]\d+:?\s*E\d+/.test(titleText)) return 'series';

  if (url.includes('/watch/') && /\b[TS]\d+:?\s*E\d+\b/.test(document.title)) return 'series';

  return 'movie';
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

/** The media on screen, or null while no source has a title for it yet. */
export function extractNetflixData(): NetflixMedia | null {
  try {
    const found = extractTitle();
    if (!found) return null;

    const { season, episode } = extractSeasonEpisode(found.text);
    const type = extractType(season, episode);
    const title = type === 'series' && !found.bare ? cleanTitle(found.text) : found.text.trim();
    const runtimeSeconds = extractRuntimeSeconds();
    const language = extractLanguage();

    const media: NetflixMedia = { title, type };

    if (language) {
      media.language = language;
    }
    if (runtimeSeconds !== undefined) {
      media.runtimeSeconds = runtimeSeconds;
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
    console.error('TaleTrack: could not extract Netflix data:', error);
    return null;
  }
}
