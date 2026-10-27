import { cookies, headers } from "next/headers";
import { parseServerEnv, resolveApiBaseUrl } from "@/env";
import { clearTokens, refreshTokens, storeTokens } from "@/lib/session";
import { ACCESS_COOKIE, REFRESH_COOKIE } from "@/lib/session/cookies";

/**
 * What the browser may ask the booking BFF for, mapped to the upstream path.
 * Everything else answers 404, so the BFF can never be used as an open proxy.
 */
const ID = "[0-9A-Za-z_]{4,64}";
const ROUTES: {
  method: "GET" | "POST";
  pattern: RegExp;
  upstream: (m: RegExpMatchArray) => string;
  idempotent?: boolean;
}[] = [
  { method: "POST", pattern: /^bookings$/, upstream: () => "/bookings", idempotent: true },
  { method: "GET", pattern: /^bookings$/, upstream: () => "/bookings" },
  {
    method: "GET",
    pattern: new RegExp(`^bookings/(${ID})$`),
    upstream: (m) => `/bookings/${m[1]}`,
  },
  {
    method: "POST",
    pattern: new RegExp(`^bookings/(${ID})/pay$`),
    upstream: (m) => `/bookings/${m[1]}/pay`,
    idempotent: true,
  },
  {
    method: "POST",
    pattern: new RegExp(`^bookings/(${ID})/pay/verify$`),
    upstream: (m) => `/bookings/${m[1]}/pay/verify`,
  },
  {
    method: "GET",
    pattern: new RegExp(`^bookings/(${ID})/cancellation-quote$`),
    upstream: (m) => `/bookings/${m[1]}/cancellation-quote`,
  },
  {
    method: "POST",
    pattern: new RegExp(`^bookings/(${ID})/cancel$`),
    upstream: (m) => `/bookings/${m[1]}/cancel`,
  },
  {
    method: "GET",
    pattern: new RegExp(`^bookings/(${ID})/tickets$`),
    upstream: (m) => `/bookings/${m[1]}/tickets`,
  },
  {
    method: "GET",
    pattern: new RegExp(`^bookings/(${ID})/itinerary\\.pdf$`),
    upstream: (m) => `/bookings/${m[1]}/itinerary.pdf`,
  },
  {
    method: "POST",
    pattern: new RegExp(`^bookings/(${ID})/disruptions/(${ID})/acknowledge$`),
    upstream: (m) => `/bookings/${m[1]}/disruptions/${m[2]}/acknowledge`,
  },
  {
    method: "GET",
    pattern: new RegExp(`^tickets/(${ID})/qr$`),
    upstream: (m) => `/tickets/${m[1]}/qr`,
  },
  {
    method: "GET",
    pattern: new RegExp(`^options/(${ID})$`),
    upstream: (m) => `/trips/options/${m[1]}`,
  },
];

const PASS_THROUGH_TYPES = ["application/pdf", "image/svg+xml"];

function problem(status: number, code: string): Response {
  return Response.json({ code }, { status, headers: { "Cache-Control": "no-store" } });
}

export type BffRequest = {
  method: string;
  path: string;
  search: string;
  origin: string | null;
  /** The `Sec-Fetch-Site` header; accepted in place of a missing `Origin`. */
  fetchSite?: string | null;
  ownOrigin: string;
  idempotencyKey: string | null;
  body: () => Promise<unknown>;
};

/** State-changing requests must say where they come from: a matching `Origin`, or `Sec-Fetch-Site: same-origin` when `Origin` is absent. */
function isSameOrigin(req: BffRequest): boolean {
  if (req.origin !== null) return req.origin === req.ownOrigin;
  return req.fetchSite === "same-origin";
}

/** Handles one browser request: allow-list, CSRF origin check, token refresh, upstream call, safe relay. */
export async function handleBookingRequest(
  req: BffRequest,
  fetchImpl: typeof fetch = fetch,
): Promise<Response> {
  const route = ROUTES.map((r) => ({
    r,
    m: r.method === req.method ? req.path.match(r.pattern) : null,
  })).find((x) => x.m);
  if (!route?.m) return problem(404, "NOT_FOUND");
  if (req.method !== "GET" && !isSameOrigin(req)) return problem(403, "FORBIDDEN");
  if (
    route.r.idempotent &&
    req.method === "POST" &&
    (!req.idempotencyKey || req.idempotencyKey.length > 128)
  ) {
    return problem(422, "IDEMPOTENCY_KEY_REQUIRED");
  }

  const jar = await cookies();
  const h = await headers();
  const forwarded = h.get("x-forwarded-for")?.split(",")[0]?.trim();
  const refresh = jar.get(REFRESH_COOKIE)?.value;
  let access = jar.get(ACCESS_COOKIE)?.value;

  const renew = async (): Promise<boolean> => {
    if (!refresh) return false;
    const pair = await refreshTokens(refresh, {
      clientIp: forwarded ?? null,
      userAgent: h.get("user-agent"),
    });
    if (!pair) return false;
    access = pair.accessToken;
    await storeTokens(pair).catch(() => undefined);
    return true;
  };

  const body = req.method === "POST" ? await req.body() : undefined;
  const url = `${resolveApiBaseUrl(parseServerEnv())}${route.r.upstream(route.m)}${req.method === "GET" ? req.search : ""}`;
  const call = () => {
    const outgoing: Record<string, string> = {
      Accept: "application/json, application/problem+json, application/pdf, image/svg+xml",
    };
    if (access) outgoing.Authorization = `Bearer ${access}`;
    if (forwarded) outgoing["X-Forwarded-For"] = forwarded;
    if (body !== undefined) outgoing["Content-Type"] = "application/json";
    if (req.idempotencyKey && route.r.idempotent) outgoing["Idempotency-Key"] = req.idempotencyKey;
    return fetchImpl(url, {
      method: req.method,
      headers: outgoing,
      cache: "no-store",
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    });
  };

  if (!access && !(await renew())) return problem(401, "AUTH_REQUIRED");
  let upstream: Response;
  try {
    upstream = await call();
    if (upstream.status === 401 && (await renew())) upstream = await call();
  } catch {
    return problem(502, "UPSTREAM_UNAVAILABLE");
  }
  if (upstream.status === 401) {
    await clearTokens().catch(() => undefined);
    return problem(401, "AUTH_REQUIRED");
  }

  const type = upstream.headers.get("content-type") ?? "";
  if (upstream.ok && PASS_THROUGH_TYPES.some((t) => type.startsWith(t))) {
    const out = new Headers({ "Content-Type": type, "Cache-Control": "private, no-store" });
    const disposition = upstream.headers.get("content-disposition");
    if (disposition) out.set("Content-Disposition", disposition);
    return new Response(upstream.body, { status: 200, headers: out });
  }
  const text = await upstream.text();
  let json: unknown = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = null;
  }
  if (json === null)
    return upstream.ok
      ? new Response(null, { status: upstream.status })
      : problem(upstream.status, "UNKNOWN");
  const out = Response.json(json, {
    status: upstream.status,
    headers: { "Cache-Control": "no-store" },
  });
  if (upstream.headers.get("idempotent-replayed")) out.headers.set("Idempotent-Replayed", "true");
  return out;
}
