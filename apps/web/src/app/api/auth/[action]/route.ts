import { NextResponse, type NextRequest } from "next/server";
import { REFRESH_COOKIE } from "@/lib/session/cookies";
import { isSameOrigin, trustedClientIp } from "@/lib/session/request";
import {
  authedCall,
  callApi,
  clearTokens,
  relay,
  storeTokens,
  type TokenPair,
} from "@/lib/session";

/** Public auth actions the browser may POST to. Everything maps 1:1 to an API endpoint. */
const PUBLIC_ACTIONS: Record<string, string> = {
  register: "/auth/register",
  "verify-email": "/auth/verify-email",
  "resend-verification": "/auth/resend-verification",
  "forgot-password": "/auth/forgot-password",
  "reset-password": "/auth/reset-password",
};

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ action: string }> },
) {
  if (!isSameOrigin(request.headers, request.nextUrl.origin))
    return NextResponse.json({ code: "FORBIDDEN" }, { status: 403 });
  const { action } = await params;
  const body: unknown = await request.json().catch(() => undefined);
  if (body === undefined) return NextResponse.json({ code: "BAD_REQUEST" }, { status: 400 });
  const ctx = {
    clientIp: trustedClientIp(request.headers),
    userAgent: request.headers.get("user-agent"),
  };

  if (action === "login") {
    const result = await callApi("/auth/login", { method: "POST", body, ...ctx });
    if (result.status !== 200) return relay(result);
    await storeTokens(result.body as TokenPair);
    return new NextResponse(null, { status: 204, headers: { "Cache-Control": "no-store" } });
  }

  if (action === "logout") {
    const refresh = request.cookies.get(REFRESH_COOKIE)?.value;
    if (refresh)
      await authedCall("/auth/logout", { method: "POST", body: { refreshToken: refresh } });
    await clearTokens();
    return new NextResponse(null, { status: 204 });
  }

  if (action === "refresh-session") {
    // After email verification the access token still says "unverified"; renew it so the claim updates.
    const refresh = request.cookies.get(REFRESH_COOKIE)?.value;
    if (!refresh) return NextResponse.json({ code: "AUTH_REQUIRED" }, { status: 401 });
    const { refreshTokens } = await import("@/lib/session/upstream");
    const pair = await refreshTokens(refresh, ctx);
    if (!pair) {
      await clearTokens();
      return NextResponse.json({ code: "AUTH_REQUIRED" }, { status: 401 });
    }
    await storeTokens(pair);
    return new NextResponse(null, { status: 204 });
  }

  const target = PUBLIC_ACTIONS[action];
  if (!target) return NextResponse.json({ code: "NOT_FOUND" }, { status: 404 });
  return relay(await callApi(target, { method: "POST", body, ...ctx }));
}
