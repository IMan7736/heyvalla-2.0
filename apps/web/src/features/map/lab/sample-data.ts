/**
 * Synthetic sample data for the map lab and tests. Coordinates are approximate public geography;
 * times, prices and ids are invented. Not dataset rows.
 */
import type { Leg, Place, PlaceRef, TripOption } from "../types";

const ref = (
  id: string,
  type: PlaceRef["type"],
  name: string,
  lng: number,
  lat: number,
  code?: string,
): PlaceRef => ({
  id,
  type,
  name: { en: name },
  geo: { lat, lng },
  ...(code ? { code } : {}),
});

export const sampleRefs = {
  maafushi: ref("jty_01SAMPLE000000000000000001", "jetty", "Maafushi Jetty", 73.4896, 3.9417),
  hulhumale: ref(
    "jty_01SAMPLE000000000000000002",
    "jetty",
    "Hulhumalé Ferry Terminal",
    73.5408,
    4.2185,
  ),
  mle: ref(
    "apt_01SAMPLE000000000000000003",
    "airport",
    "Velana International Airport",
    73.529,
    4.1918,
    "MLE",
  ),
  gan: ref(
    "apt_01SAMPLE000000000000000004",
    "airport",
    "Gan International Airport",
    73.1521,
    -0.693,
    "GAN",
  ),
  hithadhoo: ref("jty_01SAMPLE000000000000000005", "jetty", "Hithadhoo Harbour", 73.0905, -0.6027),
};

const money = { amountMinor: 0, currency: "MVR" } as const;
const leg = (
  n: number,
  mode: Leg["mode"],
  from: PlaceRef,
  to: PlaceRef,
  departAt: string,
  arriveAt: string,
): Leg => ({
  id: `leg_01SAMPLE00000000000000000${n}`,
  mode,
  providerId: "mock-lab",
  from,
  to,
  departAt,
  arriveAt,
  durationMin: Math.round((Date.parse(arriveAt) - Date.parse(departAt)) / 60_000),
  price: money,
  bookable: true,
  pricing: "fixed",
  noteCodes: [],
});

/** Ferry -> taxi -> domestic flight -> taxi, Maafushi to Hithadhoo. Times are UTC (UTC+5 local). */
export const sampleOption: TripOption = {
  id: "opt_01SAMPLE00000000000000000A" as TripOption["id"],
  departAt: "2026-10-27T03:00:00Z",
  arriveAt: "2026-10-27T10:05:00Z",
  durationMin: 425,
  transfers: 3,
  legs: [
    leg(
      1,
      "ferry",
      sampleRefs.maafushi,
      sampleRefs.hulhumale,
      "2026-10-27T03:00:00Z",
      "2026-10-27T04:10:00Z",
    ),
    leg(
      2,
      "taxi",
      sampleRefs.hulhumale,
      sampleRefs.mle,
      "2026-10-27T04:25:00Z",
      "2026-10-27T04:45:00Z",
    ),
    leg(
      3,
      "domestic_flight",
      sampleRefs.mle,
      sampleRefs.gan,
      "2026-10-27T06:40:00Z",
      "2026-10-27T08:10:00Z",
    ),
    leg(
      4,
      "taxi",
      sampleRefs.gan,
      sampleRefs.hithadhoo,
      "2026-10-27T08:30:00Z",
      "2026-10-27T10:05:00Z",
    ),
  ],
  price: { total: money, breakdown: [] },
  buffers: [],
  risk: { missProbability: 0.02, level: "low" },
  labels: ["safest"],
  score: 0.9,
  explanations: [],
  warnings: [],
};

const place = (
  id: string,
  type: Place["type"],
  name: string,
  lng: number,
  lat: number,
  extra: Partial<Place> = {},
): Place => ({
  id: id as Place["id"],
  type,
  name: { en: name },
  geo: { lat, lng },
  isHub: false,
  approximate: true,
  ...extra,
});

export const samplePlaces: Place[] = [
  place("isl_01SAMPLE0000000000000000M1", "island", "Malé", 73.5093, 4.1755, { isHub: true }),
  place("isl_01SAMPLE0000000000000000M2", "island", "Hulhumalé", 73.5408, 4.2185, { isHub: true }),
  place(
    "apt_01SAMPLE0000000000000000M3",
    "airport",
    "Velana International Airport",
    73.529,
    4.1918,
    { isHub: true, code: "MLE" },
  ),
  place("isl_01SAMPLE0000000000000000M4", "island", "Maafushi", 73.4896, 3.9417),
  place("isl_01SAMPLE0000000000000000M5", "island", "Thulusdhoo", 73.6535, 4.3748),
  place("isl_01SAMPLE0000000000000000M6", "island", "Gulhi", 73.5167, 3.9957),
  place("isl_01SAMPLE0000000000000000M7", "island", "Dhiffushi", 73.7194, 4.3558),
  place("isl_01SAMPLE0000000000000000M8", "island", "Ukulhas", 72.8604, 4.1993),
  place("isl_01SAMPLE0000000000000000M9", "island", "Rasdhoo", 72.9936, 4.2644),
  place("isl_01SAMPLE000000000000000010", "island", "Mahibadhoo", 72.9625, 3.7706),
  place("isl_01SAMPLE000000000000000011", "island", "Dhangethi", 72.9473, 3.6103),
  place("isl_01SAMPLE000000000000000012", "island", "Fulidhoo", 73.5984, 3.7014),
  place("isl_01SAMPLE000000000000000013", "island", "Thoddoo", 72.9467, 4.4361),
  place("isl_01SAMPLE000000000000000014", "island", "Felidhoo", 73.4253, 3.4519),
  place("isl_01SAMPLE000000000000000015", "island", "Kudahuvadhoo", 72.8974, 2.6742),
  place("isl_01SAMPLE000000000000000016", "island", "Naifaru", 73.3739, 5.4442),
  place("isl_01SAMPLE000000000000000017", "island", "Funadhoo", 73.5342, 5.5778, { isHub: false }),
  place("isl_01SAMPLE000000000000000018", "island", "Kulhudhuffushi", 73.069, 6.6195, {
    isHub: true,
  }),
  place("apt_01SAMPLE000000000000000019", "airport", "Kulhudhuffushi Airport", 73.0647, 6.6377, {
    code: "HDK",
  }),
  place("isl_01SAMPLE000000000000000020", "island", "Hanimaadhoo", 73.1769, 6.7519),
  place("isl_01SAMPLE000000000000000021", "island", "Ungoofaaru", 73.0222, 5.6606),
  place("isl_01SAMPLE000000000000000022", "island", "Eydhafushi", 73.0716, 5.102),
  place("isl_01SAMPLE000000000000000023", "island", "Dharavandhoo", 73.1356, 5.1568),
  place("isl_01SAMPLE000000000000000024", "island", "Fonadhoo", 73.5062, 1.8187),
  place("isl_01SAMPLE000000000000000025", "island", "Thinadhoo", 72.9957, 0.5288, { isHub: true }),
  place("isl_01SAMPLE000000000000000026", "island", "Kooddoo", 73.4803, 0.7298),
  place("isl_01SAMPLE000000000000000027", "island", "Hithadhoo", 73.0905, -0.6027, { isHub: true }),
  place("apt_01SAMPLE000000000000000028", "airport", "Gan International Airport", 73.1521, -0.693, {
    code: "GAN",
  }),
  place("isl_01SAMPLE000000000000000029", "island", "Fuvahmulah", 73.4194, -0.2993, {
    isHub: true,
  }),
  place("isl_01SAMPLE000000000000000030", "island", "Dhaandhoo", 73.5893, 0.4333, {
    approximate: true,
  }),
];
