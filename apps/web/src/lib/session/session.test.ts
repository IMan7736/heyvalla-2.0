import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import { proxy, safeReturnTo } from "@/proxy";
import { cookieAttributes } from "./cookies";
import { isSameOrigin, trustedClientIp } from "./request";
import { callApi, refreshSession, refreshTokens } from "./upstream";

function json(status: number, body?: unknown) {
  return new Response(body === undefined ? null : JSON.stringify(body), { status });
}

describe("cookieAttributes", () => {
  it("is httpOnly, lax, site-wide, and secure in production", () => {
    expect(cookieAttributes(60, true)).toEqual({
      httpOnly: true,
      secure: true,
      sameSite: "lax",
      path: "/",
      maxAge: 60,
    });
  });
});

describe("callApi", () => {
  it("sends bearer, the trusted client address with the shared key and parses problem bodies", async () => {
    vi.stubEnv("BFF_SHARED_KEY", "test-key");
    const fetchMock = vi.fn(async () => json(401, { code: "AUTH_REQUIRED" }));
    const result = await callApi(
      "/me",
      { accessToken: "abc", clientIp: "203.0.113.5" },
      fetchMock as unknown as typeof fetch,
    );
    expect(result).toMatchObject({ status: 401, body: { code: "AUTH_REQUIRED" } });
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(init.headers).toMatchObject({
      Authorization: "Bearer abc",
      "X-Dhathuru-Client-Ip": "203.0.113.5",
      "X-Dhathuru-Bff-Key": "test-key",
    });
    expect(init.headers).not.toHaveProperty("X-Forwarded-For");
    vi.unstubAllEnvs();
  });

  it("tolerates empty and non-JSON bodies", async () => {
    const empty = await callApi("/x", {}, (async () => json(204)) as unknown as typeof fetch);
    expect(empty.body).toBeNull();
    const text = await callApi(
      "/x",
      {},
      (async () => new Response("<html>", { status: 502 })) as unknown as typeof fetch,
    );
    expect(text.body).toBeNull();
  });
});

describe("refreshTokens", () => {
  it("shares one upstream call between concurrent refreshes of the same token", async () => {
    const pair = { accessToken: "a2", refreshToken: "r2", tokenType: "Bearer", expiresIn: 900 };
    const fetchMock = vi.fn(async () => json(200, pair));
    const [a, b] = await Promise.all([
      refreshTokens("same-token", {}, fetchMock as unknown as typeof fetch),
      refreshTokens("same-token", {}, fetchMock as unknown as typeof fetch),
    ]);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(a).toEqual(pair);
    expect(b).toEqual(pair);
  });

  it("returns null when the API rejects the token", async () => {
    const result = await refreshTokens("dead-token", {}, (async () =>
      json(401, { code: "INVALID_REFRESH_TOKEN" })) as unknown as typeof fetch);
    expect(result).toBeNull();
  });
});

describe("proxy (route protection and renewal)", () => {
  const request = (path: string, cookie?: string) =>
    new NextRequest(`http://localhost:3000${path}`, { headers: cookie ? { cookie } : {} });
  const pair = { accessToken: "a2", refreshToken: "r2", tokenType: "Bearer", expiresIn: 900 };

  afterEach(() => vi.unstubAllGlobals());

  it("redirects signed-out visitors to /login with a return-to", async () => {
    const response = await proxy(request("/account/profile?tab=1"));
    expect(response.status).toBe(307);
    const location = new URL(response.headers.get("location") ?? "");
    expect(location.pathname).toBe("/login");
    expect(location.searchParams.get("returnTo")).toBe("/account/profile?tab=1");
  });

  it("lets signed-in visitors and public pages through without calling the API", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    expect(
      (await proxy(request("/account", "dh_at=x; dh_rt=x"))).headers.get("location"),
    ).toBeNull();
    expect((await proxy(request("/login"))).headers.get("location")).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("renews an expired access token before the page renders and saves the new cookies (L-01)", async () => {
    const fetchMock = vi.fn(async () => json(200, pair));
    vi.stubGlobal("fetch", fetchMock);
    const response = await proxy(request("/help", "dh_rt=r1"));
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const setCookie = response.headers.getSetCookie().join("\n");
    expect(setCookie).toContain("dh_at=a2");
    expect(setCookie).toContain("dh_rt=r2");
    expect(setCookie).toMatch(/HttpOnly/i);
    // The page that renders next must see the new cookies too, not the dead one.
    expect(response.headers.get("x-middleware-request-cookie") ?? "").toContain("dh_at=a2");
  });

  it("renews on a protected route instead of sending the traveller to /login", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => json(200, { ...pair, refreshToken: "r3" })),
    );
    const response = await proxy(request("/trips", "dh_rt=r2"));
    expect(response.headers.get("location")).toBeNull();
    expect(response.headers.getSetCookie().join("\n")).toContain("dh_rt=r3");
  });

  it("drops the dead refresh cookie and asks for a sign-in when the API rejects it", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => json(401, { code: "INVALID_REFRESH_TOKEN" })),
    );
    const response = await proxy(request("/trips", "dh_rt=dead"));
    expect(new URL(response.headers.get("location") ?? "").pathname).toBe("/login");
    expect(response.headers.getSetCookie().join("\n")).toMatch(/dh_rt=;/);
  });

  it("keeps the cookies when the API is down or rate limiting, so a retry still works", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => json(503)),
    );
    const down = await proxy(request("/trips", "dh_rt=keep1"));
    expect(down.headers.get("location")).toBeNull();
    expect(down.headers.getSetCookie().join("\n")).not.toContain("dh_rt=;");
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => json(429)),
    );
    const limited = await proxy(request("/help", "dh_rt=keep2"));
    expect(limited.headers.getSetCookie().join("\n")).not.toContain("dh_rt=;");
  });
});

describe("isSameOrigin (L-12)", () => {
  const h = (init: Record<string, string>) => new Headers(init);
  it("accepts a matching Origin and rejects a different one", () => {
    expect(isSameOrigin(h({ origin: "https://app.example" }), "https://app.example")).toBe(true);
    expect(isSameOrigin(h({ origin: "https://evil.example" }), "https://app.example")).toBe(false);
  });
  it("without Origin requires Sec-Fetch-Site: same-origin", () => {
    expect(isSameOrigin(h({}), "https://app.example")).toBe(false);
    expect(isSameOrigin(h({ "sec-fetch-site": "cross-site" }), "https://app.example")).toBe(false);
    expect(isSameOrigin(h({ "sec-fetch-site": "same-origin" }), "https://app.example")).toBe(true);
  });
});

describe("trustedClientIp (L-03)", () => {
  it("prefers the edge-set header and never the first, user-supplied, forwarded entry", () => {
    expect(
      trustedClientIp(
        new Headers({ "x-real-ip": "203.0.113.9", "x-forwarded-for": "1.1.1.1, 2.2.2.2" }),
      ),
    ).toBe("203.0.113.9");
    expect(trustedClientIp(new Headers({ "x-forwarded-for": "6.6.6.6, 203.0.113.7" }))).toBe(
      "203.0.113.7",
    );
    expect(trustedClientIp(new Headers())).toBeNull();
  });
});

describe("refreshSession outcomes", () => {
  it.each([
    [401, "rejected"],
    [429, "unavailable"],
    [503, "unavailable"],
  ])("status %s is %s", async (status, kind) => {
    const outcome = await refreshSession(`tok-${status}`, {}, (async () =>
      json(status)) as unknown as typeof fetch);
    expect(outcome.kind).toBe(kind);
  });
});

describe("safeReturnTo", () => {
  it.each([
    ["/trips/abc", "/trips/abc"],
    ["//evil.example", "/"],
    ["https://evil.example", "/"],
    ["/\\evil", "/"],
    [null, "/"],
  ])("%s -> %s", (input, expected) => expect(safeReturnTo(input)).toBe(expected));
});

describe("relay (L-14)", () => {
  it("gives a bare 429 a RATE_LIMITED code and keeps Retry-After", async () => {
    const { relay } = await import("./index");
    const response = relay({
      status: 429,
      body: null,
      headers: new Headers({ "retry-after": "12" }),
    });
    expect(response.status).toBe(429);
    expect(response.headers.get("retry-after")).toBe("12");
    expect(await response.json()).toEqual({
      code: "RATE_LIMITED",
      params: { retryAfterSeconds: 12 },
    });
  });

  it("passes the API's own problem body through untouched", async () => {
    const { relay } = await import("./index");
    const problem = { code: "RATE_LIMITED", params: { retryAfterSeconds: 7 } };
    const response = relay({
      status: 429,
      body: problem,
      headers: new Headers({ "retry-after": "7" }),
    });
    expect(await response.json()).toEqual(problem);
  });

  it("labels other empty failures UPSTREAM_ERROR", async () => {
    const { relay } = await import("./index");
    const response = relay({ status: 502, body: null, headers: new Headers() });
    expect(await response.json()).toMatchObject({ code: "UPSTREAM_ERROR" });
  });
});
