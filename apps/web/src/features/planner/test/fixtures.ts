import type { TripOption } from "../lib/types";

const place = (id: string, name: string) => ({
  id,
  type: "island" as const,
  name: { en: name },
  geo: { lat: 4, lng: 73 },
});

export function makeOption(overrides: Partial<TripOption> = {}): TripOption {
  return {
    id: "trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2",
    departAt: "2026-10-27T02:30:00Z",
    arriveAt: "2026-10-27T08:00:00Z",
    durationMin: 330,
    transfers: 1,
    legs: [
      {
        id: "leg_01K8Z3M5Q7R9T2V4W6X8Y0G1H2",
        mode: "ferry",
        providerId: "static-example-ferry",
        from: place("isl_01K8Z3M5Q7R9T2V4W6X8Y0A1B2", "Maafushi"),
        to: place("jty_01K8Z3M5Q7R9T2V4W6X8Y0A1E2", "Hulhumalé Ferry Terminal"),
        departAt: "2026-10-27T02:30:00Z",
        arriveAt: "2026-10-27T04:00:00Z",
        durationMin: 90,
        price: { amountMinor: 9000, currency: "MVR" },
        bookable: true,
        pricing: "fixed",
        noteCodes: [],
      },
      {
        id: "leg_01K8Z3M5Q7R9T2V4W6X8Y0G1H3",
        mode: "domestic_flight",
        providerId: "static-example-air",
        from: place("jty_01K8Z3M5Q7R9T2V4W6X8Y0A1E2", "Hulhumalé Ferry Terminal"),
        to: place("isl_01K8Z3M5Q7R9T2V4W6X8Y0A1F2", "Kooddoo"),
        departAt: "2026-10-27T06:30:00Z",
        arriveAt: "2026-10-27T08:00:00Z",
        durationMin: 90,
        price: { amountMinor: 185000, currency: "MVR" },
        bookable: true,
        pricing: "estimate",
        noteCodes: [],
      },
    ],
    price: {
      total: { amountMinor: 194000, currency: "MVR" },
      breakdown: [
        { code: "FERRY_FARE", amount: { amountMinor: 9000, currency: "MVR" } },
        { code: "FLIGHT_FARE", amount: { amountMinor: 185000, currency: "MVR" } },
      ],
    },
    buffers: [{ afterLegIndex: 0, minutes: 150, requiredMinutes: 90, comfortMinutes: 120 }],
    risk: { missProbability: 0.03, level: "low", weakestConnectionIndex: 0 },
    labels: ["safest", "best_overall"],
    score: 87.4,
    explanations: [
      { code: "BUFFER_COMFORTABLE", params: { minutes: 150 }, severity: "good" },
      { code: "ESTIMATED_TAXI_FARE", params: {}, severity: "info" },
    ],
    warnings: [{ code: "ESTIMATED_TAXI_FARE", params: { legIndex: 1 } }],
    ...overrides,
  };
}
