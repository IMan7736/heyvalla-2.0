import { cookies, headers } from "next/headers";
import { NextResponse } from "next/server";
import type { components } from "@dhathuru/contracts";
import {
  ACCESS_COOKIE,
  REFRESH_COOKIE,
  REFRESH_MAX_AGE_SECONDS,
  cookieAttributes,
} from "./cookies";
import { trustedClientIp } from "./request";
import { callApi, refreshTokens, type TokenPair, type Upstream } from "./upstream";

export type SessionUser = components["schemas"]["User"];

async function clientContext() {
  const h = await headers();
  return { clientIp: trustedClientIp(h), userAgent: h.get("user-agent") };
}

/** True where `cookies().set` works (route handlers, server actions); false in Server Components. */
async function canWriteCookies(
  jar: Awaited<ReturnType<typeof cookies>>,
  refresh: string | undefined,
): Promise<boolean> {
  try {
    if (refresh) jar.set(REFRESH_COOKIE, refresh, cookieAttributes(REFRESH_MAX_AGE_SECONDS));
    else jar.set("dh_probe", "", { maxAge: 0, path: "/" });
    return true;
  } catch {
    return false;
  }
}

export async function storeTokens(pair: TokenPair): Promise<void> {
  const jar = await cookies();
  jar.set(ACCESS_COOKIE, pair.accessToken, cookieAttributes(pair.expiresIn));
  jar.set(REFRESH_COOKIE, pair.refreshToken, cookieAttributes(REFRESH_MAX_AGE_SECONDS));
}

export async function clearTokens(): Promise<void> {
  const jar = await cookies();
  jar.delete(ACCESS_COOKIE);
  jar.delete(REFRESH_COOKIE);
}

/**
 * Calls the API as the signed-in user. Refreshes the token server side when the access token is missing or rejected
 * and retries once. Returns `null` when there is no usable session (cookies are cleared in that case).
 */
export async function authedCall(
  path: string,
  init: { method?: string; body?: unknown } = {},
): Promise<Upstream | null> {
  const jar = await cookies();
  const ctx = await clientContext();
  let access = jar.get(ACCESS_COOKIE)?.value;
  const refresh = jar.get(REFRESH_COOKIE)?.value;

  // Server Components cannot set cookies. Rotating a refresh token there would throw the new one away and the old one
  // would then count as stolen, so renewal only happens where the result can be saved. proxy.ts renews before pages
  // render; route handlers renew here.
  const writable = await canWriteCookies(jar, refresh);

  const renew = async (): Promise<boolean> => {
    if (!refresh || !writable) return false;
    const pair = await refreshTokens(refresh, ctx);
    if (!pair) return false;
    access = pair.accessToken;
    await storeTokens(pair);
    return true;
  };

  if (!access && !(await renew())) return null;
  let result = await callApi(path, { ...init, accessToken: access, ...ctx });
  if (result.status === 401 && (await renew())) {
    result = await callApi(path, { ...init, accessToken: access, ...ctx });
  }
  if (result.status === 401) {
    if (writable) await clearTokens();
    return null;
  }
  return result;
}

/** Typed current user, or `null` when signed out. */
export async function getSession(): Promise<SessionUser | null> {
  const result = await authedCall("/me");
  return result && result.status === 200 ? (result.body as SessionUser) : null;
}

/** Turns an upstream result into a response for the browser (problem+json passes through, tokens never do). */
export function relay(result: Upstream): NextResponse {
  const retryAfter = result.headers.get("retry-after");
  const headers: Record<string, string> = { "Cache-Control": "no-store" };
  if (retryAfter) headers["Retry-After"] = retryAfter;
  if (result.status === 204) return new NextResponse(null, { status: result.status, headers });
  if (result.body === null) {
    // A reply with no problem body (a proxy error page, a bare 429) still needs a code the UI can show.
    return NextResponse.json(
      {
        code: result.status === 429 ? "RATE_LIMITED" : "UPSTREAM_ERROR",
        params: retryAfter ? { retryAfterSeconds: Number(retryAfter) || 0 } : {},
      },
      { status: result.status, headers },
    );
  }
  return NextResponse.json(result.body, { status: result.status, headers });
}

export { callApi, refreshTokens };
export type { TokenPair };
