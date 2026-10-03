/**
 * Decodes a JWT's payload (base64url-encoded UTF-8 JSON) without checking its signature.
 *
 * @remarks
 * Works in both the Edge runtime and Node, where `atob` and `TextDecoder` are global.
 *
 * @param token - The raw token.
 * @returns The parsed claims.
 * @throws When the token is malformed.
 */
export function decodeJwtPayload<T = Record<string, unknown>>(token: string): T {
  const seg = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const padded = seg + '='.repeat((4 - (seg.length % 4)) % 4);
  const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
  return JSON.parse(new TextDecoder().decode(bytes)) as T;
}

/**
 * Cheap, signature-less JWT check: is the token well-formed and not yet expired?
 *
 * @remarks
 * Only used for routing decisions (middleware, layout chrome); the backend still validates
 * the signature on every request.
 *
 * @param token - The raw access token, if any.
 * @returns `true` when the token decodes and its `exp` claim lies in the future.
 */
export function isJwtValid(token: string | undefined | null): boolean {
  if (!token) return false;
  try {
    const payload = decodeJwtPayload<{ exp?: number }>(token);
    return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now();
  } catch {
    return false;
  }
}
