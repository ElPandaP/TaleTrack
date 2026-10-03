/**
 * Reads what is playing on a Netflix page and turns it into a {@link NetflixMedia}.
 *
 * Used by the content script, which only decides when to call {@link extractNetflixData} and what
 * to do with the result. Netflix has no public API, so each piece of information is taken from
 * several sources in order of reliability:
 *
 * 1. The player's own metadata and the Falcor cache, read from the page by the injected script
 *    and fetched with {@link requestNetflixData}.
 * 2. The URL (the /watch/ id).
 * 3. The DOM: the on-screen title bar, series-only elements, `document.title`, JSON-LD and the
 *    `<video>` element.
 *
 * @module
 */

import type {
  NetflixEpisodeRef,
  NetflixMedia,
  NetflixPageData,
  NetflixSeasonRef,
  NetflixVideoMetadata,
} from './types';
import { AUTO_POLL_MS, debug } from './config';

/** Season and episode numbers, each null when unknown. */
type SeasonEpisode = { season: number | null; episode: number | null };

/** The last data received from the injected page script via {@link requestNetflixData}. */
let netflixData: NetflixPageData | null = null;

/** Watch id that was on screen when `netflixData` arrived. */
let netflixDataVideoId: string | null = null;

/**
 * Last title read from the on-screen title bar, per watch id. The bar is a real source for the
 * title (Netflix's player metadata isn't always there) but is only visible for a few seconds, so
 * the cache keeps it after the bar fades.
 */
const titleCache = new Map<string, string>();

/**
 * Seconds before the credits start from which a video counts as finished. Twice the poll interval,
 * so a periodic check can't step over the window.
 */
export const CREDITS_MARGIN_SECONDS = (AUTO_POLL_MS / 1000) * 2;

/**
 * An element's text with its pieces separated by spaces. Netflix's title bar puts the show name,
 * the episode marker and the episode name in separate elements, which `textContent` runs together
 * ("ShowE4Episode name"), hiding the marker from the word-bounded patterns.
 */
function elementText(el: Element | null): string {
  if (!el) return '';
  const parts: string[] = [];
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const text = node.textContent?.trim();
    if (text) parts.push(text);
  }
  return parts.join(' ');
}

/** The video id in the current /watch/<id> URL, or null when not on a player page. */
export function getWatchVideoId(): string | null {
  return window.location.href.match(/\/watch\/(\d+)/)?.[1] ?? null;
}

/**
 * The page data from {@link requestNetflixData}, or null when it belongs to another video (the
 * injected script did not answer after the player moved on, e.g. to the next episode).
 */
function pageData(): NetflixPageData | null {
  return netflixDataVideoId === getWatchVideoId() ? netflixData : null;
}

/**
 * Where the credits start, in seconds, from the last data read with {@link requestNetflixData}.
 *
 * @returns The offset from the Falcor cache or, failing that, from the player metadata (per
 * episode for a series, on the video for a movie); null when neither has it.
 */
export function getCreditsOffsetSeconds(): number | null {
  const fromFalcor = pageData()?.creditsOffset;
  if (typeof fromFalcor === 'number' && fromFalcor > 0) return fromFalcor;

  // The player metadata has it per episode for a series, on the video itself for a movie.
  const fromMeta = currentEpisodeFromMetadata()?.episode.creditsOffset ?? getMetaVideo()?.creditsOffset;
  return typeof fromMeta === 'number' && fromMeta > 0 ? fromMeta : null;
}

/**
 * Asks the injected page script for Netflix's player and Falcor data and keeps the answer for the
 * extraction functions of this module.
 *
 * @returns A promise that resolves once the page has answered, or after 1 second without an
 * answer (the previous data is then kept, but only used while the same video is on screen).
 */
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
        netflixDataVideoId = getWatchVideoId();
        debug('page data from injected script:', event.data.success ? netflixData : `none (${event.data.error ?? 'nothing found'})`);
        finish();
      }
    };

    window.addEventListener('message', onMessage);
    window.postMessage({ type: 'GET_NETFLIX_DATA', requestId }, window.location.origin);

    timer = setTimeout(() => {
      debug('injected script did not answer within 1s (is injected.js running in the page?)');
      finish();
    }, 1000);
  });
}

/** Total runtime of the movie / episode in seconds, from the `<video>` element or the Falcor cache. */
function extractRuntimeSeconds(): number | undefined {
  // 1. The <video> element (content scripts share the page DOM)
  const video = document.querySelector('video');
  if (video && Number.isFinite(video.duration) && video.duration > 0) return Math.round(video.duration);

  // 2. Falcor runtime (seconds), before the <video> knows its length
  const falcorRuntime = pageData()?.runtime;
  if (typeof falcorRuntime === 'number' && Number.isFinite(falcorRuntime) && falcorRuntime > 0) {
    return Math.round(falcorRuntime);
  }

  return undefined;
}

/** The player's stored metadata for the video being watched. */
function getMetaVideo(): NetflixVideoMetadata | undefined {
  return pageData()?.metadata?.video;
}

/** The episode being watched and its season, located in the Netflix player metadata. */
function currentEpisodeFromMetadata(): { season: NetflixSeasonRef; episode: NetflixEpisodeRef } | null {
  const video = getMetaVideo();
  if (!video || !Array.isArray(video.seasons)) return null;

  const currentId = video.currentEpisode ?? video.episodeId;
  if (!currentId) return null;

  for (const season of video.seasons) {
    // Ids come as numbers or strings depending on the build.
    const episode = (season.episodes ?? []).find((e) => String(e.id) === String(currentId));
    if (episode) return { season, episode };
  }
  return null;
}

/** Season and episode numbers of the current episode, from the Netflix player metadata. */
function seasonEpisodeFromMetadata(): SeasonEpisode | null {
  const current = currentEpisodeFromMetadata();
  if (!current) return null;
  debug('current episode in player metadata:', current.episode);
  return {
    season: current.season.seq ?? current.season.season ?? null,
    episode: current.episode.seq ?? current.episode.episode ?? null,
  };
}

/** A full episode marker such as "T1:E2" or "S1:E2", word-bounded so "Se7en" does not match. */
const EPISODE_MARKER = /\b[TS]\d+:?\s*E\d+\b/;

/**
 * Whether a title text carries a full episode marker ("T1:E2", "S1:E2"), which means a series.
 *
 * @param text - Title text that may contain a marker.
 */
export function hasEpisodeMarker(text: string): boolean {
  return EPISODE_MARKER.test(text);
}

/**
 * Season and episode numbers of what is playing, from Netflix's own data: the player metadata or,
 * failing that, the Falcor summary. Both null when neither has them.
 */
function extractSeasonEpisode(): SeasonEpisode {
  const fromMeta = seasonEpisodeFromMetadata();
  if (fromMeta && (fromMeta.season !== null || fromMeta.episode !== null)) {
    debug('season/episode from player metadata:', fromMeta);
    return fromMeta;
  }

  const summary = pageData()?.summary;
  if (summary?.type === 'episode' && summary.season && summary.episode) {
    debug('season/episode from Falcor summary:', summary.season, summary.episode);
    return { season: summary.season, episode: summary.episode };
  }

  debug('no source had season/episode numbers');
  return { season: null, episode: null };
}

/**
 * Strips episode-number/subtitle noise off a title scraped from the page, keeping just the show
 * name. Only for series: a movie's own ":" or "-" is part of its name, and titles that come from
 * Netflix's metadata are already bare.
 *
 * @param rawTitle - Title text scraped from the page.
 * @returns The show name.
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
 * The title of what is playing, from the first source that has one: player metadata, Falcor
 * summary, the on-screen title bar (or its cached copy), `document.title`, then JSON-LD.
 *
 * @returns The title and whether it is already bare (straight from Netflix's data) or scraped
 * text that may still carry episode noise. Null when no source has one yet.
 */
function extractTitle(): { text: string; bare: boolean } | null {
  // Most reliable: Netflix's own player metadata and Falcor summary, available for
  // the whole session, unlike the on-screen title bar (which only the DOM
  // selectors below can see, and which fades out a few seconds into playback).
  const metaTitle = getMetaVideo()?.title;
  if (typeof metaTitle === 'string' && metaTitle.trim()) {
    debug('title from player metadata:', metaTitle);
    return { text: metaTitle.trim(), bare: true };
  }

  const summaryTitle = pageData()?.summary?.title;
  if (typeof summaryTitle === 'string' && summaryTitle.trim()) {
    debug('title from Falcor summary:', summaryTitle);
    return { text: summaryTitle.trim(), bare: true };
  }

  const watchId = getWatchVideoId();
  const selectors = [
    '.title-title',
    '[data-uia="video-title"]',
    '[data-uia="title-name"]',
    'h1[class*="title"]',
    '.ellipsize-text h4',
    '.video-title',
  ];
  for (const selector of selectors) {
    const el = document.querySelector(selector);
    const found = elementText(el);
    if (found) {
      debug(`title from DOM selector "${selector}":`, found, el?.outerHTML);
      if (watchId) titleCache.set(watchId, found);
      return { text: found, bare: false };
    }
  }

  // The bar may be gone but have been caught earlier for this exact video
  // (e.g. right when playback started).
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
 * Movie vs series, tried in order of reliability: season/episode numbers, player metadata,
 * series-only DOM elements, then episode markers in the title texts.
 *
 * @param season - Season number found by `extractSeasonEpisode`, if any.
 * @param episode - Episode number found by `extractSeasonEpisode`, if any.
 */
function extractType(season: number | null, episode: number | null): 'movie' | 'series' {
  if (season !== null || episode !== null) return 'series';

  // Netflix player metadata is explicit about show vs movie
  const metaVideo = getMetaVideo();
  if (metaVideo?.type === 'show' || Array.isArray(metaVideo?.seasons)) return 'series';
  if (metaVideo?.type === 'movie') return 'movie';

  const seriesIndicators = [
    '.episodes-container',
    '[data-uia="season-selector"]',
    '.season-selector',
    '.episode-selector',
  ];
  if (seriesIndicators.some((selector) => document.querySelector(selector))) return 'series';

  if (hasEpisodeMarker(elementText(document.querySelector('[data-uia="video-title"]')))) return 'series';
  if (getWatchVideoId() && hasEpisodeMarker(document.title)) return 'series';

  return 'movie';
}

/**
 * Netflix UI language, e.g. "es" or "en", read from `<html lang="es-ES">`.
 * Only "es"/"en" are recognised server-side for TMDB enrichment; anything
 * else is sent as-is and simply ignored there.
 */
function extractLanguage(): string | null {
  const lang = document.documentElement.lang;
  if (!lang) return null;
  return (lang.split('-')[0] ?? lang).toLowerCase();
}

/**
 * The media on screen: title, movie or series, season and episode, runtime and UI language.
 *
 * @remarks
 * Call {@link requestNetflixData} first so the page data is up to date.
 *
 * @returns The media, or null while no source has a title for it yet.
 */
export function extractNetflixData(): NetflixMedia | null {
  try {
    const found = extractTitle();
    if (!found) return null;

    const { season, episode } = extractSeasonEpisode();
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

    debug('extracted media:', media);
    return media;
  } catch (error) {
    console.error('TaleTrack: could not extract Netflix data:', error);
    return null;
  }
}
