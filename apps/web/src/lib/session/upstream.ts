import { parseServerEnv, resolveApiBaseUrl } from "@/env";

export type TokenPair = {
  accessToken: string;
  refreshToken: string;
  tokenType: "Bearer";
  expiresIn: number;
};

export type Upstream = { status: number; body: unknown; headers: Headers };

/** Calls the .NET API from the server. `clientIp` is forwarded so rate limits apply per real client. */
export async function callApi(
  path: string,
  init: {
    method?: string;
    body?: unknown;
    accessToken?: string;
    clientIp?: string | null;
    userAgent?: string | null;
  } = {},
  fetchImpl: typeof fetch = fetch,
): Promise<Upstream> {
  const headers: Record<string, string> = { Accept: "application/json, application/problem+json" };
  if (init.body !== undefined) headers["Content-Type"] = "application/json";
  if (init.accessToken) headers.Authorization = `Bearer ${init.accessToken}`;
  if (init.clientIp) {
    // The API honours this header only together with the shared key, so a caller cannot choose its own rate-limit bucket.
    headers["X-Dhathuru-Client-Ip"] = init.clientIp;
    const key = process.env.BFF_SHARED_KEY;
    if (key) headers["X-Dhathuru-Bff-Key"] = key;
  }
  if (init.userAgent) headers["User-Agent"] = init.userAgent;
  const response = await fetchImpl(`${resolveApiBaseUrl(parseServerEnv())}${path}`, {
    method: init.method ?? "GET",
    headers,
    ...(init.body !== undefined ? { body: JSON.stringify(init.body) } : {}),
    cache: "no-store",
  });
  const text = await response.text();
  let body: unknown = null;
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = null;
    }
  }
  return { status: response.status, body, headers: response.headers };
}

export type RefreshOutcome =
  | { kind: "ok"; pair: TokenPair }
  | { kind: "rejected" } // the API says this refresh token is no good: the session is over
  | { kind: "unavailable" }; // outage, rate limit or network error: keep the cookies and try again later

const inflight = new Map<string, Promise<RefreshOutcome>>();

/**
 * Exchanges a refresh token. Concurrent calls with the same token share one request, because the API treats a
 * second use of a refresh token as theft and kills the session.
 */
export function refreshSession(
  refreshToken: string,
  ctx: { clientIp?: string | null; userAgent?: string | null } = {},
  fetchImpl: typeof fetch = fetch,
): Promise<RefreshOutcome> {
  const existing = inflight.get(refreshToken);
  if (existing) return existing;
  const promise = callApi(
    "/auth/refresh",
    { method: "POST", body: { refreshToken }, ...ctx },
    fetchImpl,
  )
    .then((r): RefreshOutcome => {
      if (r.status === 200) return { kind: "ok", pair: r.body as TokenPair };
      return r.status === 400 || r.status === 401 || r.status === 403
        ? { kind: "rejected" }
        : { kind: "unavailable" };
    })
    .catch((): RefreshOutcome => ({ kind: "unavailable" }))
    .finally(() => {
      setTimeout(() => inflight.delete(refreshToken), 5000).unref?.();
    });
  inflight.set(refreshToken, promise);
  return promise;
}

/** Like {@link refreshSession} but collapses everything except success to `null`. */
export async function refreshTokens(
  refreshToken: string,
  ctx: { clientIp?: string | null; userAgent?: string | null } = {},
  fetchImpl: typeof fetch = fetch,
): Promise<TokenPair | null> {
  const outcome = await refreshSession(refreshToken, ctx, fetchImpl);
  return outcome.kind === "ok" ? outcome.pair : null;
}
