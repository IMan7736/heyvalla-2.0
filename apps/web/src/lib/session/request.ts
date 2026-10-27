/**
 * Request facts the BFF relies on. Kept free of Next.js imports so it can be unit tested.
 */

/**
 * State-changing requests must prove they come from our own pages: a matching `Origin`, or, when a client sends no
 * `Origin`, the browser's `Sec-Fetch-Site: same-origin`. A request with neither is refused.
 */
export function isSameOrigin(headers: Headers, expectedOrigin: string): boolean {
  const origin = headers.get("origin");
  if (origin !== null) return origin === expectedOrigin;
  return headers.get("sec-fetch-site") === "same-origin";
}

/**
 * The end user's address as seen by the platform in front of us, never a value the user typed.
 * `x-vercel-forwarded-for` and `x-real-ip` are set by the edge; failing that we take the LAST `x-forwarded-for`
 * entry, which is the one our own proxy appended (earlier entries are client-supplied).
 */
export function trustedClientIp(headers: Headers): string | null {
  const edge = headers.get("x-vercel-forwarded-for") ?? headers.get("x-real-ip");
  if (edge) return edge.split(",")[0]?.trim() || null;
  const chain = headers.get("x-forwarded-for")?.split(",");
  return chain?.[chain.length - 1]?.trim() || null;
}
