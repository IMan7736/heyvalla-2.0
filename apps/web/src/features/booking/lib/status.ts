import type { Booking, BookingStatus, Leg } from "./types";

export type Tone = "success" | "caution" | "danger" | "neutral" | "lagoon";

export const STATUS_TONE: Record<BookingStatus, Tone> = {
  draft: "neutral",
  held: "caution",
  payment_pending: "caution",
  confirmed: "success",
  failed: "danger",
  expired: "neutral",
  partially_cancelled: "caution",
  cancelled: "neutral",
  refunded: "neutral",
};

const FAILURE_KEYS = new Set([
  "CARD_DECLINED",
  "INSUFFICIENT_FUNDS",
  "VERIFICATION_FAILED",
  "PAYMENT_TIMEOUT",
  "GATEWAY_ERROR",
  "INVALID_PAYMENT_TOKEN",
  "LEG_UNAVAILABLE",
  "LEG_CONFIRM_FAILED",
  "HOLD_EXPIRED",
]);

/** Maps an API failure code to a message key under `booking.failure`, falling back to a generic one. */
export function failureKey(code: string | null | undefined): string {
  return code && FAILURE_KEYS.has(code) ? code : "UNKNOWN";
}

/** Is this booking one the traveller can still act on (pay or cancel)? */
export function isActive(status: BookingStatus): boolean {
  return (
    status === "held" ||
    status === "payment_pending" ||
    status === "confirmed" ||
    status === "partially_cancelled"
  );
}

export function firstDeparture(booking: Booking): string | null {
  const times = booking.legs.map((l) => l.leg.departAt).sort();
  return times[0] ?? null;
}

export function lastArrival(booking: Booking): string | null {
  const times = booking.legs.map((l) => l.leg.arriveAt).sort();
  return times[times.length - 1] ?? null;
}

export function isUpcoming(booking: Booking, now: number): boolean {
  const end = lastArrival(booking);
  if (booking.status === "held" || booking.status === "payment_pending") return true;
  if (
    booking.status === "failed" ||
    booking.status === "expired" ||
    booking.status === "cancelled" ||
    booking.status === "refunded"
  )
    return false;
  return end === null || new Date(end).getTime() >= now;
}

export function routeSummary(legs: { leg: Leg }[]): { from: string; to: string } | null {
  const first = legs[0]?.leg;
  const last = legs[legs.length - 1]?.leg;
  return first && last ? { from: first.from.name.en, to: last.to.name.en } : null;
}

export function openDisruptions(booking: Booking) {
  return booking.disruptions.filter((d) => d.state === "open");
}

/** Pay-on-board legs are shown but not charged. */
export function payOnBoardLines(booking: Booking) {
  return booking.priceBreakdown.filter((l) => l.payOnBoard);
}

/** A short, readable reference for a booking id ("bkg_01K8Z3...H1J2" -> "H1J2K5P9"): the last eight characters. */
export function shortRef(id: string): string {
  return id.slice(-8).toUpperCase();
}

export function sortTrips(bookings: Booking[], upcoming: boolean): Booking[] {
  const key = (b: Booking) => firstDeparture(b) ?? b.createdAt;
  return [...bookings].sort((a, b) =>
    upcoming ? key(a).localeCompare(key(b)) : key(b).localeCompare(key(a)),
  );
}

/**
 * After a pay request with no clear answer: has the server settled? True once the booking left the in-flight
 * states, or a payment newer than `baseline` reached a verdict (so an older failed attempt is never mistaken for it).
 */
export function paymentSettled(booking: Booking | undefined, baseline: string | null): boolean {
  if (!booking) return false;
  if (booking.status !== "payment_pending" && booking.status !== "held") return true;
  const payment = booking.payment;
  return (
    payment !== undefined &&
    payment !== null &&
    payment.id !== baseline &&
    payment.status !== "pending"
  );
}
