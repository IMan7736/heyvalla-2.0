import { describe, expect, it, vi, beforeEach } from "vitest";

const store = new Map<string, string>();
vi.mock("next/headers", () => ({
  cookies: async () => ({
    get: (k: string) => (store.has(k) ? { value: store.get(k) } : undefined),
    set: () => undefined,
    delete: () => undefined,
  }),
  headers: async () => new Headers({ "x-forwarded-for": "203.0.113.7" }),
}));
vi.mock("@/env", () => ({
  parseServerEnv: () => ({}),
  resolveApiBaseUrl: () => "http://api.test/v1",
}));
vi.mock("@/lib/session", () => ({
  clearTokens: async () => undefined,
  storeTokens: async () => undefined,
  refreshTokens: vi.fn(async () => null),
}));

import { handleBookingRequest, type BffRequest } from "./bff";

const base: BffRequest = {
  method: "GET",
  path: "bookings",
  search: "?limit=50",
  origin: "http://localhost:3105",
  ownOrigin: "http://localhost:3105",
  idempotencyKey: null,
  body: async () => ({}),
};

describe("booking BFF", () => {
  beforeEach(() => {
    store.clear();
    store.set("dh_at", "access-token");
  });

  it("answers 404 for anything outside the allow-list", async () => {
    const fetchImpl = vi.fn();
    for (const path of [
      "tickets/verify",
      "dev/provider-events",
      "bookings/bkg_1234/../../me",
      "me",
    ]) {
      const res = await handleBookingRequest(
        { ...base, path },
        fetchImpl as unknown as typeof fetch,
      );
      expect(res.status).toBe(404);
    }
    expect(fetchImpl).not.toHaveBeenCalled();
  });

  it("rejects cross-origin writes", async () => {
    const fetchImpl = vi.fn();
    const res = await handleBookingRequest(
      { ...base, method: "POST", origin: "https://evil.example", idempotencyKey: "k" },
      fetchImpl as unknown as typeof fetch,
    );
    expect(res.status).toBe(403);
    expect(fetchImpl).not.toHaveBeenCalled();
  });

  it("rejects a write with no Origin unless the browser says Sec-Fetch-Site: same-origin", async () => {
    for (const fetchSite of [undefined, null, "cross-site", "same-site", "none"]) {
      const fetchImpl = vi.fn();
      const res = await handleBookingRequest(
        { ...base, method: "POST", origin: null, fetchSite, idempotencyKey: "k" },
        fetchImpl as unknown as typeof fetch,
      );
      expect(res.status).toBe(403);
      expect(fetchImpl).not.toHaveBeenCalled();
    }
    const ok = await handleBookingRequest(
      { ...base, method: "POST", origin: null, fetchSite: "same-origin", idempotencyKey: "k" },
      vi.fn(async () => new Response("{}", { status: 200 })) as unknown as typeof fetch,
    );
    expect(ok.status).not.toBe(403);
  });

  it("requires an Idempotency-Key to create or pay", async () => {
    const fetchImpl = vi.fn();
    const create = await handleBookingRequest(
      { ...base, method: "POST" },
      fetchImpl as unknown as typeof fetch,
    );
    const pay = await handleBookingRequest(
      { ...base, method: "POST", path: "bookings/bkg_ABCD1234/pay" },
      fetchImpl as unknown as typeof fetch,
    );
    expect(create.status).toBe(422);
    expect(pay.status).toBe(422);
    expect(fetchImpl).not.toHaveBeenCalled();
  });

  it("forwards the bearer token, idempotency key and query, and relays problem+json", async () => {
    const fetchImpl = vi.fn(
      async () =>
        new Response(JSON.stringify({ code: "LEG_UNAVAILABLE", params: { legIndex: 1 } }), {
          status: 409,
          headers: { "content-type": "application/problem+json" },
        }),
    );
    const res = await handleBookingRequest(
      {
        ...base,
        method: "POST",
        idempotencyKey: "key-1",
        body: async () => ({ optionId: "trp_X" }),
      },
      fetchImpl as unknown as typeof fetch,
    );
    expect(res.status).toBe(409);
    expect(await res.json()).toEqual({ code: "LEG_UNAVAILABLE", params: { legIndex: 1 } });
    const [url, init] = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("http://api.test/v1/bookings");
    const headers = init.headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer access-token");
    expect(headers["Idempotency-Key"]).toBe("key-1");
    expect(JSON.parse(init.body as string)).toEqual({ optionId: "trp_X" });
  });

  it("passes the query string on GET and streams a PDF through", async () => {
    const fetchImpl = vi.fn(
      async () =>
        new Response("%PDF-1.4", {
          status: 200,
          headers: {
            "content-type": "application/pdf",
            "content-disposition": 'attachment; filename="x.pdf"',
          },
        }),
    );
    const res = await handleBookingRequest(
      { ...base, path: "bookings/bkg_ABCD1234/itinerary.pdf", search: "" },
      fetchImpl as unknown as typeof fetch,
    );
    expect(res.headers.get("content-type")).toBe("application/pdf");
    expect(await res.text()).toBe("%PDF-1.4");
  });

  it("answers 401 without a session", async () => {
    store.clear();
    const res = await handleBookingRequest(base, vi.fn() as unknown as typeof fetch);
    expect(res.status).toBe(401);
  });
});
