import { NextResponse, type NextRequest } from "next/server";
import {
  ACCESS_COOKIE,
  REFRESH_COOKIE,
  REFRESH_MAX_AGE_SECONDS,
  cookieAttributes,
} from "@/lib/session/cookies";
import { trustedClientIp } from "@/lib/session/request";
import { refreshSession } from "@/lib/session/upstream";

/** Routes that need a signed-in user. Unauthenticated visitors are sent to /login with a return-to. */
const PROTECTED_PREFIXES = ["/account", "/trips", "/booking", "/checkout"];

export function safeReturnTo(value: string | null): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.includes("\\"))
    return "/";
  return value;
}

function redirectToLogin(request: NextRequest): NextResponse {
  const { pathname, search } = request.nextUrl;
  const url = request.nextUrl.clone();
  url.pathname = "/login";
  url.search = `?returnTo=${encodeURIComponent(pathname + search)}`;
  return NextResponse.redirect(url);
}

/**
 * Runs before every page. The access cookie lives 15 minutes; when it has gone but the refresh cookie is still there,
 * the session is renewed HERE, because this is the place where the new cookies can be written to the browser and
 * handed to the page that is about to render. (Server Components cannot set cookies, so renewing there would rotate the
 * refresh token and lose the new one, which the API then treats as a stolen token.)
 */
export async function proxy(request: NextRequest) {
  const isProtected = PROTECTED_PREFIXES.some(
    (p) => request.nextUrl.pathname === p || request.nextUrl.pathname.startsWith(`${p}/`),
  );
  const access = request.cookies.get(ACCESS_COOKIE)?.value;
  const refresh = request.cookies.get(REFRESH_COOKIE)?.value;

  if (!access && refresh) {
    const outcome = await refreshSession(refresh, {
      clientIp: trustedClientIp(request.headers),
      userAgent: request.headers.get("user-agent"),
    });

    if (outcome.kind === "ok") {
      const { pair } = outcome;
      request.cookies.set(ACCESS_COOKIE, pair.accessToken);
      request.cookies.set(REFRESH_COOKIE, pair.refreshToken);
      const response = NextResponse.next({ request: { headers: request.headers } });
      response.cookies.set(ACCESS_COOKIE, pair.accessToken, cookieAttributes(pair.expiresIn));
      response.cookies.set(
        REFRESH_COOKIE,
        pair.refreshToken,
        cookieAttributes(REFRESH_MAX_AGE_SECONDS),
      );
      return response;
    }

    if (outcome.kind === "rejected") {
      // The session is really over: drop the dead cookie so nothing keeps trying it.
      request.cookies.delete(REFRESH_COOKIE);
      const response = isProtected
        ? redirectToLogin(request)
        : NextResponse.next({ request: { headers: request.headers } });
      response.cookies.delete(REFRESH_COOKIE);
      return response;
    }
    // unavailable: keep the cookies; the next navigation tries again.
  }

  if (isProtected && !access && !refresh) return redirectToLogin(request);
  return NextResponse.next();
}

export const config = { matcher: ["/((?!_next|api|.*\\..*).*)"] };
