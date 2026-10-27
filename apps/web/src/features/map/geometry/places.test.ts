import { describe, expect, it } from "vitest";
import { placesToFeatures, searchPlaces } from "./places";
import type { Place } from "../types";

const p = (name: string, extra: Partial<Place> = {}): Place => ({
  id: `isl_${name}` as Place["id"],
  type: "island",
  name: { en: name },
  geo: { lat: 4, lng: 73 },
  isHub: false,
  ...extra,
});
const places = [
  p("Malé", { isHub: true }),
  p("Hulhumalé", { isHub: true }),
  p("Maafushi"),
  p("Velana Airport", { type: "airport", code: "MLE" }),
];

describe("searchPlaces", () => {
  it("ignores case and diacritics and ranks prefixes first", () => {
    expect(searchPlaces(places, "male").map((x) => x.name.en)).toEqual(["Malé", "Hulhumalé"]);
  });
  it("matches whole words inside names and IATA codes", () => {
    expect(searchPlaces(places, "airport")[0]!.name.en).toBe("Velana Airport");
    expect(searchPlaces(places, "mle")[0]!.name.en).toBe("Velana Airport");
  });
  it("returns nothing for blank or unknown queries and honours the limit", () => {
    expect(searchPlaces(places, "  ")).toEqual([]);
    expect(searchPlaces(places, "zzz")).toEqual([]);
    expect(searchPlaces(places, "m", 2)).toHaveLength(2);
  });
});

describe("placesToFeatures", () => {
  it("carries id, name, hub and service counts", () => {
    const fc = placesToFeatures(places, { [places[0]!.id]: 12 });
    expect(fc.features).toHaveLength(4);
    expect(fc.features[0]!.properties).toMatchObject({ name: "Malé", hub: true, services: 12 });
    expect(fc.features[1]!.properties).toMatchObject({ services: 0 });
  });
});
