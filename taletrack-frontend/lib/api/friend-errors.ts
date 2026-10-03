import { ApiError } from './client';

/** Backend error codes for a rejected friend request that have their own translated message. */
const SEND_ERROR_CODES = new Set([
  'already_friends',
  'already_pending',
  'reverse_pending',
  'self',
  'target_not_found',
]);

/**
 * Maps a failed "send friend request" error to a translation key (`friends.sendError.*`).
 *
 * @param err - Whatever the request threw; only an {@link ApiError} with a known `code` gets a specific key.
 * @returns The key for that code, or `friends.sendError.generic` for anything unrecognized.
 */
export function sendErrorKey(err: unknown): string {
  const code = err instanceof ApiError ? err.code : undefined;
  return code && SEND_ERROR_CODES.has(code)
    ? `friends.sendError.${code}`
    : 'friends.sendError.generic';
}
