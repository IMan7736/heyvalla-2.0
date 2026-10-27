import { describe, expect, it } from "vitest";
import { addRecent, MAX_RECENTS, readRecents, recordRecent, type Recent } from "./recents";

const mk = (id: string, kind: Recent["kind"] = "place", at = 1): Recent => ({
  kind,
  id,
  label: id,
  href: `/x/${id}`,
  at,
});

function memoryStorage(initial?: string) {
  let value: string | null = initial ?? null;
  return {
    getItem: () => value,
    setItem: (_: string, v: string) => {
      value = v;
    },
  };
}

describe("addRecent", () => {
  it("puts the newest first and de-duplicates", () => {
    const list = addRecent([mk("a"), mk("b")], mk("b", "place", 9));
    expect(list.map((r) => r.id)).toEqual(["b", "a"]);
    expect(list[0]?.at).toBe(9);
  });

  it("treats the same id of a different kind as distinct", () => {
    expect(addRecent([mk("a", "place")], mk("a", "trip"))).toHaveLength(2);
  });

  it("caps the list", () => {
    let list: Recent[] = [];
    for (let i = 0; i < MAX_RECENTS + 4; i++) list = addRecent(list, mk(`p${i}`));
    expect(list).toHaveLength(MAX_RECENTS);
    expect(list[0]?.id).toBe(`p${MAX_RECENTS + 3}`);
  });
});

describe("readRecents / recordRecent", () => {
  it("round-trips through storage", () => {
    const storage = memoryStorage();
    recordRecent({ kind: "place", id: "isl_1", label: "Malé", href: "/p/isl_1" }, 5, storage);
    expect(readRecents(storage)).toEqual([
      { kind: "place", id: "isl_1", label: "Malé", href: "/p/isl_1", at: 5 },
    ]);
  });

  it("returns empty for corrupt or invalid data", () => {
    expect(readRecents(memoryStorage("{not json"))).toEqual([]);
    expect(readRecents(memoryStorage(JSON.stringify([{ kind: "place" }])))).toEqual([]);
    expect(readRecents(null)).toEqual([]);
  });

  it("rejects off-site hrefs", () => {
    const bad = [{ kind: "place", id: "x", label: "x", href: "https://evil.example", at: 1 }];
    expect(readRecents(memoryStorage(JSON.stringify(bad)))).toEqual([]);
  });
});
