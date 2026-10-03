// Unit tests for the pure text helpers of netflix-extract: episode markers and title cleanup.
import { describe, expect, test } from 'bun:test';
import { cleanTitle, hasEpisodeMarker } from '../src/netflix-extract';

describe('hasEpisodeMarker', () => {
  test('detects a Spanish marker', () => {
    expect(hasEpisodeMarker('Stranger Things T1:E2 Capítulo dos')).toBe(true);
  });

  test('detects an English marker', () => {
    expect(hasEpisodeMarker('Stranger Things S3:E8')).toBe(true);
  });

  test('ignores a bare episode number without a season', () => {
    expect(hasEpisodeMarker('Cosmos E5')).toBe(false);
  });

  test('does not read an episode into a movie title', () => {
    expect(hasEpisodeMarker('Se7en')).toBe(false);
    expect(hasEpisodeMarker('SE7EN')).toBe(false);
    expect(hasEpisodeMarker('Blade Runner 2049')).toBe(false);
    expect(hasEpisodeMarker('Extraction 2')).toBe(false);
  });
});

describe('cleanTitle', () => {
  test('strips the episode marker and what follows', () => {
    expect(cleanTitle('Stranger Things T1:E2 Capítulo dos')).toBe('Stranger Things');
    expect(cleanTitle('Stranger Things S1:E2 Chapter Two')).toBe('Stranger Things');
  });

  test('strips a bare episode marker and what follows', () => {
    expect(cleanTitle('BoJack Horseman E4 Entre Zoe y Zelda')).toBe('BoJack Horseman');
  });

  test('strips a marker run together with the show name', () => {
    expect(cleanTitle('Stranger ThingsT1:E2Capítulo dos')).toBe('Stranger Things');
  });

  test('strips a subtitle after a colon', () => {
    expect(cleanTitle('Dark: Winden')).toBe('Dark');
  });

  test('keeps a colon subtitle that is part of the name', () => {
    expect(cleanTitle('Star Trek: The Next Generation')).toBe('Star Trek: The Next Generation');
    expect(cleanTitle('Sherlock: La novia abominable')).toBe('Sherlock: La novia abominable');
  });

  test('strips a subtitle after a spaced dash', () => {
    expect(cleanTitle('Black Mirror - Bandersnatch')).toBe('Black Mirror');
  });

  test('leaves a plain title alone', () => {
    expect(cleanTitle('  Breaking Bad ')).toBe('Breaking Bad');
    expect(cleanTitle('Se7en')).toBe('Se7en');
    expect(cleanTitle('Spider-Man')).toBe('Spider-Man');
  });
});
