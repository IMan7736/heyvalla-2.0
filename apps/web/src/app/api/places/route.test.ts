import { afterEach, describe, expect, it, vi } from "vitest";

const get = vi.fn();
vi.mock("@/lib/api-client", () => ({ createApiClient: () => ({ GET: get }) }));

import { GET } from "./route";

afterEach(() => get.mockReset());

describe("GET /api/places", () => {
  it("forwards a clamped query and returns the API body unchanged", async () => {
    const body = { items: [{ id: "isl_x" }], page: { limit: 6, nextCursor: null } };
    get.mockResolvedValue({ data: body, response: { status: 200 } });

    const res = await GET(
      new Request(`http://localhost/api/places?query=${"a".repeat(200)}&limit=999`),
    );

    expect(await res.json()).toEqual(body);
    const call = get.mock.calls[0]?.[1] as { params: { query: { query: string; limit: number } } };
    expect(call.params.query.query).toHaveLength(80);
    expect(call.params.query.limit).toBe(20);
  });

  it("passes upstream failures through as a machine code", async () => {
    get.mockResolvedValue({ data: undefined, response: { status: 422 } });
    const res = await GET(new Request("http://localhost/api/places?query=x"));
    expect(res.status).toBe(422);
    expect(await res.json()).toEqual({ code: "UPSTREAM_ERROR" });
  });

  it("answers 502 when the API is unreachable", async () => {
    get.mockRejectedValue(new Error("down"));
    const res = await GET(new Request("http://localhost/api/places?query=maa"));
    expect(res.status).toBe(502);
  });
});
