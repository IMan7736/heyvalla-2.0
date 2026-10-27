import type { Booking } from "../lib/types";

const place = (id: string, name: string) => ({
  id,
  type: "island",
  name: { en: name },
  geo: { lat: 4, lng: 73 },
});

export function bookingFixture(patch: Partial<Booking> = {}): Booking {
  return {
    id: "bkg_01K8Z3M5Q7R9T2V4W6X8Y0H1J2",
    status: "held",
    optionId: "trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2",
    legs: [
      {
        id: "bkl_01K8Z3M5Q7R9T2V4W6X8Y0J1K2",
        legIndex: 0,
        status: "held",
        providerReference: null,
        failureCode: null,
        amount: { amountMinor: 9000, currency: "MVR" },
        leg: {
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
      },
    ],
    passengers: [
      { id: "pax_01K8Z3M5Q7R9T2V4W6X8Y0Q1R2", fullName: "Aishath Naseem", passengerType: "adult" },
    ],
    total: { amountMinor: 9000, currency: "MVR" },
    holdExpiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
    payment: null,
    createdAt: "2026-10-26T20:00:00Z",
    updatedAt: "2026-10-26T20:00:00Z",
    failureCode: null,
    priceBreakdown: [
      {
        code: "FARE",
        amount: { amountMinor: 9000, currency: "MVR" },
        legIndex: 0,
        payOnBoard: false,
      },
    ],
    disruptions: [],
    ...patch,
  } as Booking;
}
