import { describe, expect, it, vi } from "vitest";
import { searchPlaces } from "./places-search";

const signal = new AbortController().signal;
const body = {
  items: [
    { id: "isl_1", type: "island", name: { en: "Maafushi", dv: "x" }, approximate: true },
    { id: "apt_1", type: "airport", name: { en: "Velana" } },
  ],
};

describe("searchPlaces", () => {
  it("skips the network for short queries", async () => {
    const fetcher = vi.fn();
    expect(await searchPlaces(" m ", signal, fetcher)).toEqual([]);
    expect(fetcher).not.toHaveBeenCalled();
  });

  it("maps the response and defaults approximate", async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify(body)));
    const result = await searchPlaces("maa", signal, fetcher);
    expect(result).toEqual([
      { id: "isl_1", type: "island", name: "Maafushi", approximate: true },
      { id: "apt_1", type: "airport", name: "Velana", approximate: false },
    ]);
    expect(String(fetcher.mock.calls[0]?.[0])).toBe("/api/places?query=maa&limit=6");
  });

  it("throws on HTTP errors and malformed payloads", async () => {
    await expect(
      searchPlaces("maa", signal, vi.fn().mockResolvedValue(new Response("", { status: 502 }))),
    ).rejects.toThrow();
    await expect(
      searchPlaces("maa", signal, vi.fn().mockResolvedValue(new Response('{"items":5}'))),
    ).rejects.toThrow();
  });
});
