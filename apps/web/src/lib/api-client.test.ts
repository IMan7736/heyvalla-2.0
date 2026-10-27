import { describe, expect, it, vi } from "vitest";
import { createApiClient } from "./api-client";

describe("createApiClient", () => {
  it("calls typed contract paths on the configured base URL", async () => {
    const fetchMock = vi.fn(async (request: Request) => {
      expect(request.url).toBe("http://localhost:4010/health");
      return Response.json({ status: "ok", version: "0.1.0", time: "2026-10-26T20:00:00Z" });
    });
    const client = createApiClient({
      baseUrl: "http://localhost:4010",
      fetch: fetchMock as unknown as typeof fetch,
    });

    const { data, error } = await client.GET("/health");

    expect(error).toBeUndefined();
    expect(data?.status).toBe("ok");
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it("defaults to the Prism mock while API_MODE is unset", async () => {
    const seen: Request[] = [];
    const fetchMock = async (request: Request) => {
      seen.push(request);
      return Response.json({ items: [], page: { limit: 50, nextCursor: null } });
    };
    const client = createApiClient({ fetch: fetchMock as unknown as typeof fetch });
    await client.GET("/places", { params: { query: { query: "maa" } } });
    expect(seen[0]?.url).toBe("http://localhost:4010/places?query=maa");
  });
});
