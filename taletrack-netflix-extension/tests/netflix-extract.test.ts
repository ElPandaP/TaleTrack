import { describe, expect, test } from 'bun:test';
import { cleanTitle, parseEpisodeMarker } from '../src/netflix-extract';

describe('parseEpisodeMarker', () => {
  test('reads season and episode from a Spanish marker', () => {
    expect(parseEpisodeMarker('Stranger Things T1:E2 Capítulo dos')).toEqual({ season: 1, episode: 2 });
  });

  test('reads season and episode from an English marker', () => {
    expect(parseEpisodeMarker('Stranger Things S3:E8')).toEqual({ season: 3, episode: 8 });
  });

  test('reads a bare episode number', () => {
    expect(parseEpisodeMarker('Cosmos E5')).toEqual({ season: null, episode: 5 });
  });

  test('does not read an episode into a movie title', () => {
    expect(parseEpisodeMarker('Se7en')).toBeNull();
    expect(parseEpisodeMarker('SE7EN')).toBeNull();
    expect(parseEpisodeMarker('Blade Runner 2049')).toBeNull();
    expect(parseEpisodeMarker('Extraction 2')).toBeNull();
  });
});

describe('cleanTitle', () => {
  test('strips the episode marker and what follows', () => {
    expect(cleanTitle('Stranger Things T1:E2 Capítulo dos')).toBe('Stranger Things');
    expect(cleanTitle('Stranger Things S1:E2 Chapter Two')).toBe('Stranger Things');
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
